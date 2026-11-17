using System.Net;
using System.Text.Json;
using CombinationGenerator.Api.IntegrationTests.Infrastructure;

namespace CombinationGenerator.Api.IntegrationTests;

/// <summary>Wire format of every endpoint (docs/API_CONTRACT.md, "Endpoints" and "Conventions").</summary>
public sealed class ContractTests : IClassFixture<ApiFactory>
{
    private readonly ApiClient _api;

    public ContractTests(ApiFactory factory)
    {
        _api = new ApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task Start_N3_Returns200WithExactShape()
    {
        using var response = await _api.PostStartRawAsync("""{ "n": 3 }""");

        var json = await ApiClient.ReadJsonAsync(response, HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        json.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("sessionId", "n", "totalCount");
        Guid.TryParse(Json.RequireString(json, "sessionId"), out var id).Should().BeTrue();
        id.Should().NotBeEmpty();
        json.GetProperty("n").ValueKind.Should().Be(JsonValueKind.Number);
        json.GetProperty("n").GetInt32().Should().Be(3);
        Json.RequireString(json, "totalCount").Should().Be("6");
    }

    [Theory]
    [InlineData(1, "1")]
    [InlineData(2, "2")]
    [InlineData(10, "3628800")]
    [InlineData(13, "6227020800")]
    [InlineData(19, "121645100408832000")]
    [InlineData(20, "2432902008176640000")]
    public async Task Start_ValidN_TotalCountIsExactFactorialString(int n, string expected)
    {
        var start = await _api.StartAsync(n);

        start.N.Should().Be(n);
        start.TotalCount.Should().Be(expected);
    }

    [Fact]
    public async Task Start_CalledTwice_ReturnsDifferentSessions()
    {
        var first = await _api.StartAsync(3);

        var second = await _api.StartAsync(3);

        second.SessionId.Should().NotBe(first.SessionId);
    }

    [Fact]
    public async Task Next_FirstCall_Returns200WithExactShape()
    {
        var start = await _api.StartAsync(3);

        using var response = await _api.GetNextRawAsync(start.SessionId.ToString());

        var json = await ApiClient.ReadJsonAsync(response, HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        json.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("index", "permutation", "hasMore", "message");
        var next = NextResult.From(json);
        next.Should().BeEquivalentTo(new NextResult("1", [1, 2, 3], true, null));
    }

    [Fact]
    public async Task Next_WhenExhausted_ReturnsNullsAndMessageWithAllPropertiesPresent()
    {
        var start = await _api.StartAsync(1);
        await _api.NextAsync(start.SessionId);

        using var response = await _api.GetNextRawAsync(start.SessionId.ToString());

        var json = await ApiClient.ReadJsonAsync(response, HttpStatusCode.OK);
        json.GetProperty("index").ValueKind.Should().Be(JsonValueKind.Null);
        json.GetProperty("permutation").ValueKind.Should().Be(JsonValueKind.Null);
        json.GetProperty("hasMore").GetBoolean().Should().BeFalse();
        json.GetProperty("message").GetString().Should().Be("No more combinations");
    }

    [Fact]
    public async Task All_N3AfterTwoNexts_ReturnsContractExampleExactly()
    {
        var start = await _api.StartAsync(3);
        await _api.NextAsync(start.SessionId);
        await _api.NextAsync(start.SessionId);

        using var response = await _api.GetAllRawAsync(start.SessionId.ToString(), "pageSize=2&page=1");

        var json = await ApiClient.ReadJsonAsync(response, HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        json.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            "items", "page", "pageSize", "totalPages", "totalItems", "firstIndex", "message");
        json.GetProperty("pageSize").ValueKind.Should().Be(JsonValueKind.Number);
        foreach (var item in json.GetProperty("items").EnumerateArray())
        {
            item.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("index", "permutation");
        }

        PageResult.From(json).Should().BeEquivalentTo(new PageResult(
            [new PageItem("3", [2, 1, 3]), new PageItem("4", [2, 3, 1])],
            Page: "1",
            PageSize: 2,
            TotalPages: "2",
            TotalItems: "4",
            FirstIndex: "3",
            Message: null));
    }

    [Fact]
    public async Task All_DefaultQuery_UsesPageSize50AndPage1()
    {
        var start = await _api.StartAsync(5);

        using var response = await _api.GetAllRawAsync(start.SessionId.ToString(), query: null);

        var page = PageResult.From(await ApiClient.ReadJsonAsync(response, HttpStatusCode.OK));
        page.PageSize.Should().Be(50);
        page.Page.Should().Be("1");
        page.Items.Should().HaveCount(50);
        page.TotalItems.Should().Be("120");
        page.TotalPages.Should().Be("3");
        page.FirstIndex.Should().Be("1");
        page.Items[0].Should().BeEquivalentTo(new PageItem("1", [1, 2, 3, 4, 5]));
    }

    [Fact]
    public async Task All_RightAfterStart_PageOneStartsAtFirstPermutation()
    {
        var start = await _api.StartAsync(3);

        var page = await _api.AllAsync(start.SessionId, pageSize: 4, page: 1);

        page.FirstIndex.Should().Be("1");
        page.TotalItems.Should().Be("6");
        page.TotalPages.Should().Be("2");
        page.Items.Select(i => i.Permutation).Should().BeEquivalentTo(Reference.N3.Take(4), o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task All_LastPartialPage_ContainsOnlyRemainingItems()
    {
        var start = await _api.StartAsync(3);

        var page = await _api.AllAsync(start.SessionId, pageSize: 4, page: 2);

        page.Items.Select(i => i.Index).Should().Equal("5", "6");
        page.Items.Select(i => i.Permutation).Should().BeEquivalentTo(Reference.N3.Skip(4), o => o.WithStrictOrdering());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task All_PagingThroughWholeSequence_MatchesReferenceOracle(int n)
    {
        var reference = Reference.AllPermutations(n);
        var start = await _api.StartAsync(n);
        const int pageSize = 7;
        var collected = new List<PageItem>();

        var first = await _api.AllAsync(start.SessionId, pageSize, 1);
        collected.AddRange(first.Items);
        var totalPages = long.Parse(first.TotalPages);
        for (long p = 2; p <= totalPages; p++)
        {
            collected.AddRange((await _api.AllAsync(start.SessionId, pageSize, p)).Items);
        }

        collected.Select(i => i.Index).Should().Equal(Enumerable.Range(1, reference.Count).Select(i => i.ToString()));
        collected.Select(i => i.Permutation).Should().BeEquivalentTo(reference, o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task Next_WalkingWholeSequenceN4_MatchesReferenceOracleAndHasMoreFlags()
    {
        var reference = Reference.AllPermutations(4);
        var start = await _api.StartAsync(4);

        for (var i = 0; i < reference.Count; i++)
        {
            var next = await _api.NextAsync(start.SessionId);

            next.Index.Should().Be((i + 1).ToString());
            next.Permutation.Should().Equal(reference[i]);
            next.HasMore.Should().Be(i < reference.Count - 1);
            next.Message.Should().BeNull();
        }

        var after = await _api.NextAsync(start.SessionId);
        after.Should().BeEquivalentTo(new NextResult(null, null, false, "No more combinations"));
    }

    [Fact]
    public async Task UnknownRoute_Returns404ProblemDetails()
    {
        using var response = await _api.Http.GetAsync("/api/permutations/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }
}
