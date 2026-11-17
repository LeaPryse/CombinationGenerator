using System.Net;
using System.Text;
using CombinationGenerator.Api.IntegrationTests.Infrastructure;

namespace CombinationGenerator.Api.IntegrationTests;

/// <summary>
/// <c>POST /api/permutations/back</c> (docs/API_CONTRACT.md v1.1, endpoint 4 and "Back (returning from All mode)").
/// </summary>
public sealed class BackEndpointTests : IClassFixture<ApiFactory>
{
    private const string NoMore = "No more combinations";

    private readonly ApiFactory _factory;
    private readonly ApiClient _api;

    public BackEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _api = new ApiClient(factory.CreateClient());
    }

    // ---- status codes / shape ---------------------------------------------

    [Fact]
    public async Task Back_ExistingSession_Returns204WithEmptyBody()
    {
        var id = (await _api.StartAsync(3)).SessionId;

        using var response = await _api.PostBackRawAsync(id.ToString());

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    [InlineData("8f6c2a3e-4b1d-4c8e-9f2a-1d3b5e7a9c0")]
    public async Task Back_MissingOrInvalidSessionHeader_Returns400ForHeader(string? header)
    {
        using var response = await _api.PostBackRawAsync(header);

        await ProblemAssert.ValidationAsync(response, "X-Session-Id", ProblemAssert.HeaderMessage);
    }

    [Fact]
    public async Task Back_UnknownSession_Returns404()
    {
        var unknown = Guid.NewGuid();

        using var response = await _api.PostBackRawAsync(unknown.ToString());

        await ProblemAssert.NotFoundAsync(response, unknown);
    }

    [Fact]
    public async Task Back_EmptyGuid_Returns404()
    {
        using var response = await _api.PostBackRawAsync(Guid.Empty.ToString());

        await ProblemAssert.NotFoundAsync(response, Guid.Empty);
    }

    [Fact]
    public async Task Back_ExpiredSession_Returns404()
    {
        var id = (await _api.StartAsync(3)).SessionId;
        await _api.NextAsync(id);
        await _api.AllAsync(id, 2, 1);

        _factory.Clock.Advance(TimeSpan.FromMinutes(31));
        using var response = await _api.PostBackRawAsync(id.ToString());

        await ProblemAssert.NotFoundAsync(response, id);
    }

    [Fact]
    public async Task Back_ResetsSlidingTimer()
    {
        var id = (await _api.StartAsync(3)).SessionId;

        _factory.Clock.Advance(TimeSpan.FromMinutes(20));
        await _api.BackAsync(id);
        _factory.Clock.Advance(TimeSpan.FromMinutes(20)); // 40 min since start, 20 since back

        (await _api.NextAsync(id)).Index.Should().Be("1");
    }

    [Theory]
    [InlineData("{}", "application/json")]
    [InlineData("null", "application/json")]
    public async Task Back_WithJsonBody_IsIgnoredAndReturns204(string body, string mediaType)
    {
        // Angular's HttpClient.post(url, {}) / post(url, null) may send a body; the endpoint has no body.
        var id = (await _api.StartAsync(3)).SessionId;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiClient.BasePath}/back")
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        };
        request.Headers.Add(ApiClient.SessionHeader, id.ToString());

        using var response = await _api.Http.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---- semantics -----------------------------------------------------------

    [Fact]
    public async Task Back_ContractScenario_BackThenAll_StartsRightAfterLastShownItem()
    {
        // Contract table "Back, then All again" (n = 3, page size 2).
        var id = (await _api.StartAsync(3)).SessionId;

        (await _api.NextAsync(id)).Index.Should().Be("1");

        var p1 = await _api.AllAsync(id, 2, 1);
        p1.Items.Should().BeEquivalentTo(
            new[] { new PageItem("2", Reference.N3[1]), new PageItem("3", Reference.N3[2]) }, o => o.WithStrictOrdering());
        p1.FirstIndex.Should().Be("2");
        p1.TotalItems.Should().Be("5");

        var p2 = await _api.AllAsync(id, 2, 2);
        p2.Items.Should().BeEquivalentTo(
            new[] { new PageItem("4", Reference.N3[3]), new PageItem("5", Reference.N3[4]) }, o => o.WithStrictOrdering());

        await _api.BackAsync(id);

        var again = await _api.AllAsync(id, 2, 1);
        again.Should().BeEquivalentTo(
            new PageResult([new PageItem("6", Reference.N3[5])], "1", 2, "1", "1", "6", null),
            o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task Back_WithoutBack_AllReusesStaleAnchor()
    {
        // Contract: "Without step 4 the server would still be in All mode and step 5 would return #2,#3 again".
        var id = (await _api.StartAsync(3)).SessionId;
        await _api.NextAsync(id);
        await _api.AllAsync(id, 2, 1);
        await _api.AllAsync(id, 2, 2);

        var again = await _api.AllAsync(id, 2, 1);

        again.FirstIndex.Should().Be("2");
        again.TotalItems.Should().Be("5");
        again.Items.Select(i => i.Index).Should().Equal("2", "3");
    }

    [Fact]
    public async Task Back_ThenNext_ContinuesRightAfterLastShownItem()
    {
        var id = (await _api.StartAsync(3)).SessionId;
        await _api.NextAsync(id);                 // #1
        await _api.AllAsync(id, 2, 1);           // #2,#3 → position #3

        await _api.BackAsync(id);

        (await _api.NextAsync(id)).Should().BeEquivalentTo(new NextResult("4", Reference.N3[3], true, null));
    }

    [Fact]
    public async Task Back_AfterLastPage_NextSaysNoMore_AllIsEmpty()
    {
        var id = (await _api.StartAsync(3)).SessionId;
        await _api.AllAsync(id, 3, 2);           // #4..#6 → position #6

        await _api.BackAsync(id);

        (await _api.AllAsync(id, 3, 1)).Should().BeEquivalentTo(new PageResult([], "1", 3, "0", "0", null, NoMore));
        (await _api.NextAsync(id)).Should().BeEquivalentTo(new NextResult(null, null, false, NoMore));
    }

    [Fact]
    public async Task Back_DoesNotMovePosition()
    {
        var id = (await _api.StartAsync(4)).SessionId;
        await _api.NextAsync(id);                 // #1
        await _api.AllAsync(id, 5, 2);           // #7..#11 → position #11

        await _api.BackAsync(id);
        await _api.BackAsync(id);

        (await _api.NextAsync(id)).Index.Should().Be("12");
    }

    [Fact]
    public async Task Back_IsIdempotent_RepeatedBackChangesNothing()
    {
        var id = (await _api.StartAsync(3)).SessionId;
        await _api.NextAsync(id);                 // #1
        await _api.AllAsync(id, 2, 1);           // #2,#3 → position #3

        for (var i = 0; i < 5; i++)
        {
            await _api.BackAsync(id);
        }

        var page = await _api.AllAsync(id, 2, 1);
        page.FirstIndex.Should().Be("4");
        page.TotalItems.Should().Be("3");
    }

    [Fact]
    public async Task Back_OutsideAllMode_RightAfterStart_ChangesNothing()
    {
        var id = (await _api.StartAsync(3)).SessionId;

        await _api.BackAsync(id);

        (await _api.NextAsync(id)).Index.Should().Be("1");
    }

    [Fact]
    public async Task Back_OutsideAllMode_AfterNext_ChangesNothing()
    {
        var id = (await _api.StartAsync(3)).SessionId;
        await _api.NextAsync(id);                 // #1
        await _api.NextAsync(id);                 // #2

        await _api.BackAsync(id);

        var page = await _api.AllAsync(id, 2, 1);
        page.FirstIndex.Should().Be("3");
        page.Items.Select(i => i.Index).Should().Equal("3", "4");
    }

    [Fact]
    public async Task Back_AfterEmptyAllPage_PositionUnchanged()
    {
        var id = (await _api.StartAsync(1)).SessionId;
        await _api.NextAsync(id);                 // #1 (the only one)
        (await _api.AllAsync(id, 2, 1)).Items.Should().BeEmpty();

        await _api.BackAsync(id);

        (await _api.NextAsync(id)).Message.Should().Be(NoMore);
    }

    [Fact]
    public async Task Back_TwoAllVisits_EachStartsAfterPreviousVisitLastItem()
    {
        // Next, All p1, Back, All p1, Back, All p1 … walks n=3 in pages of 2 without gaps or overlaps.
        var id = (await _api.StartAsync(3)).SessionId;
        var seen = new List<string>();

        for (var visit = 0; visit < 3; visit++)
        {
            var page = await _api.AllAsync(id, 2, 1);
            seen.AddRange(page.Items.Select(i => i.Index));
            await _api.BackAsync(id);
        }

        seen.Should().Equal("1", "2", "3", "4", "5", "6");
    }

    [Fact]
    public async Task Back_DoesNotAffectOtherSessions()
    {
        var a = (await _api.StartAsync(3)).SessionId;
        var b = (await _api.StartAsync(3)).SessionId;
        await _api.NextAsync(a);
        await _api.NextAsync(b);
        await _api.AllAsync(a, 2, 1);
        await _api.AllAsync(b, 2, 1);

        await _api.BackAsync(a);

        (await _api.AllAsync(a, 2, 1)).FirstIndex.Should().Be("4", "a left All mode, new anchor after #3");
        (await _api.AllAsync(b, 2, 1)).FirstIndex.Should().Be("2", "b is still in All mode with anchor #1");
    }

    [Fact]
    public async Task Back_ManyParallelCalls_AllReturn204AndPositionUnchanged()
    {
        var id = (await _api.StartAsync(5)).SessionId;
        await _api.AllAsync(id, 10, 1);          // #1..#10 → position #10

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => _api.PostBackRawAsync(id.ToString())));
        try
        {
            responses.Select(r => r.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.NoContent);
        }
        finally
        {
            foreach (var r in responses)
            {
                r.Dispose();
            }
        }

        (await _api.NextAsync(id)).Index.Should().Be("11");
    }
}
