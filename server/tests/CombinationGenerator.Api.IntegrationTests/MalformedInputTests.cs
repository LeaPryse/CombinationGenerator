using System.Net;
using CombinationGenerator.Api.IntegrationTests.Infrastructure;

namespace CombinationGenerator.Api.IntegrationTests;

/// <summary>
/// Input that cannot be bound at all (contract v1.1, "Errors"): the 400 must have the contract shape – keys the
/// client knows (<c>n</c>, <c>pageSize</c>, <c>page</c>), never framework keys like <c>$.n</c> or <c>request</c>,
/// and the contract messages.
/// </summary>
public sealed class MalformedInputTests : IClassFixture<ApiFactory>
{
    private readonly ApiClient _api;

    public MalformedInputTests(ApiFactory factory)
    {
        _api = new ApiClient(factory.CreateClient());
    }

    // ---- start body ----------------------------------------------------------

    [Theory]
    [InlineData("""{ "n": "abc" }""")]
    [InlineData("""{ "n": 3.5 }""")]
    [InlineData("""{ "n": true }""")]
    [InlineData("""{ "n": [3] }""")]
    [InlineData("""{ "n": { "value": 3 } }""")]
    [InlineData("""{ "n": 99999999999 }""")]
    [InlineData("""{ "n": null }""")]
    [InlineData("""{ "n": 3""")]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("[3]")]
    [InlineData("3")]
    [InlineData("\"n\"")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{ "m": 3 }""")]
    public async Task Start_MalformedOrMissingN_Returns400KeyedN(string body)
    {
        using var response = await _api.PostStartRawAsync(body);

        await ProblemAssert.ValidationAsync(response, "n", ProblemAssert.NMessage);
    }

    [Fact]
    public async Task Start_NoBody_Returns400KeyedN()
    {
        using var response = await _api.PostStartRawAsync(jsonBody: null);

        await ProblemAssert.ValidationAsync(response, "n", ProblemAssert.NMessage);
    }

    [Theory]
    [InlineData("""{ "N": 3 }""")]
    [InlineData("""{ "n": 3, "extra": "ignored" }""")]
    public async Task Start_CaseInsensitiveOrExtraProperties_AreAccepted(string body)
    {
        // System.Text.Json web defaults: case-insensitive names, unknown properties ignored.
        using var response = await _api.PostStartRawAsync(body);

        var json = await ApiClient.ReadJsonAsync(response, HttpStatusCode.OK);
        json.GetProperty("n").GetInt32().Should().Be(3);
    }

    // ---- all query -------------------------------------------------------------

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("99999999999")]
    [InlineData("1e3")]
    [InlineData("")]
    public async Task All_PageSizeNotAnInt32_Returns400KeyedPageSizeWithContractMessage(string pageSize)
    {
        var id = (await _api.StartAsync(3)).SessionId;

        using var response = await _api.GetAllRawAsync(id.ToString(), $"pageSize={pageSize}&page=1");

        if (pageSize.Length == 0)
        {
            // An empty value is "absent" for ASP.NET Core binding → default 50 is acceptable too.
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return;
            }
        }

        await ProblemAssert.ValidationAsync(response, "pageSize", ProblemAssert.PageSizeMessage);
    }

    [Theory]
    [InlineData("xyz")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("1e3")]
    [InlineData("9223372036854775808")]
    [InlineData("-9223372036854775809")]
    public async Task All_PageNotAnInt64_Returns400KeyedPageWithContractMessage(string page)
    {
        var id = (await _api.StartAsync(3)).SessionId;

        using var response = await _api.GetAllRawAsync(id.ToString(), $"pageSize=2&page={page}");

        await ProblemAssert.ValidationAsync(response, "page", ProblemAssert.PageNotIntegerMessage);
    }

    [Fact]
    public async Task All_PageSizeAndPageBothUnparsable_ListsBothFields()
    {
        var id = (await _api.StartAsync(3)).SessionId;

        using var response = await _api.GetAllRawAsync(id.ToString(), "pageSize=abc&page=xyz");

        await ProblemAssert.ValidationAsync(
            response,
            ("pageSize", ProblemAssert.PageSizeMessage),
            ("page", ProblemAssert.PageNotIntegerMessage));
    }

    [Fact]
    public async Task All_PageSizeAndPageBothOutOfRange_ListsBothFields()
    {
        // Contract: "If several query values are invalid at once, errors lists each offending field".
        var id = (await _api.StartAsync(3)).SessionId;

        using var response = await _api.GetAllRawAsync(id.ToString(), "pageSize=0&page=0");

        await ProblemAssert.ValidationAsync(
            response,
            ("pageSize", ProblemAssert.PageSizeMessage),
            ("page", ProblemAssert.PageTooSmallMessage));
    }

    [Fact]
    public async Task All_PageSizeUnparsableAndPageOutOfRange_ListsBothFields()
    {
        var id = (await _api.StartAsync(3)).SessionId;

        using var response = await _api.GetAllRawAsync(id.ToString(), "pageSize=abc&page=0");

        await ProblemAssert.ValidationAsync(
            response,
            ("pageSize", ProblemAssert.PageSizeMessage),
            ("page", ProblemAssert.PageTooSmallMessage));
    }

    [Fact]
    public async Task All_PageSizeOutOfRangeAndPageUnparsable_ListsBothFields()
    {
        var id = (await _api.StartAsync(3)).SessionId;

        using var response = await _api.GetAllRawAsync(id.ToString(), "pageSize=0&page=xyz");

        await ProblemAssert.ValidationAsync(
            response,
            ("pageSize", ProblemAssert.PageSizeMessage),
            ("page", ProblemAssert.PageNotIntegerMessage));
    }

    [Fact]
    public async Task All_UnparsablePage_DoesNotTouchSession()
    {
        // Contract: calls rejected before the session is looked up do not touch it (no anchor, no position change).
        var id = (await _api.StartAsync(3)).SessionId;
        await _api.NextAsync(id); // #1

        using (var rejected = await _api.GetAllRawAsync(id.ToString(), "pageSize=2&page=xyz"))
        {
            rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        (await _api.NextAsync(id)).Index.Should().Be("2");
        (await _api.AllAsync(id, 2, 1)).FirstIndex.Should().Be("3");
    }
}
