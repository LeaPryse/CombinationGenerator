using System.Net;
using System.Text.Json;
using CombinationGenerator.Api.IntegrationTests.Infrastructure;

namespace CombinationGenerator.Api.IntegrationTests;

/// <summary>Error responses (docs/API_CONTRACT.md, "Errors"): status codes and RFC 7807 ProblemDetails shape.</summary>
public sealed class ErrorTests : IClassFixture<ApiFactory>
{
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string ValidationTitle = "One or more validation errors occurred.";
    private const string HeaderMessage = "The X-Session-Id header is required and must be a GUID.";

    private readonly ApiClient _api;

    public ErrorTests(ApiFactory factory)
    {
        _api = new ApiClient(factory.CreateClient());
    }

    // ---- start: n -------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public async Task Start_NOutOfRange_Returns400ValidationProblemForN(int n)
    {
        using var response = await _api.PostStartRawAsync($$"""{ "n": {{n}} }""");

        await AssertValidationProblemAsync(response, "n", "n must be between 1 and 20.");
    }

    [Fact]
    public async Task Start_EmptyObject_Returns400ValidationProblemForN()
    {
        using var response = await _api.PostStartRawAsync("{}");

        await AssertValidationProblemAsync(response, "n", "n must be between 1 and 20.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{ "n": "abc" }""")]
    [InlineData("""{ "n": 3.5 }""")]
    [InlineData("""{ "n": 99999999999 }""")]
    [InlineData("""{ "n": null }""")]
    [InlineData("""{ "n": 3""")]
    [InlineData("[3]")]
    public async Task Start_MalformedBody_Returns400ProblemDetails(string body)
    {
        using var response = await _api.PostStartRawAsync(body);

        // Contract: "The same response is returned when n is missing or the body is malformed … The errors key is always n."
        await AssertValidationProblemAsync(response, "n", "n must be between 1 and 20.");
    }

    [Fact]
    public async Task Start_NoBodyAtAll_Returns400ProblemDetails()
    {
        using var response = await _api.PostStartRawAsync(jsonBody: null);

        // Contract: "400 if the body is missing/malformed". A request with no body has no Content-Type.
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Start_NonJsonContentType_ReturnsClientErrorProblemDetails()
    {
        using var response = await _api.PostStartRawAsync("n=3", "application/x-www-form-urlencoded");

        // 415 is the framework behaviour; either 400 or 415 is an acceptable client error, but it must be ProblemDetails.
        ((int)response.StatusCode).Should().BeOneOf(400, 415);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    // ---- X-Session-Id -----------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-guid")]
    [InlineData("12345")]
    [InlineData("8f6c2a3e-4b1d-4c8e-9f2a-1d3b5e7a9c0")]
    public async Task Next_MissingOrInvalidSessionHeader_Returns400ForHeader(string? header)
    {
        using var response = await _api.GetNextRawAsync(header);

        await AssertValidationProblemAsync(response, "X-Session-Id", HeaderMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task All_MissingOrInvalidSessionHeader_Returns400ForHeader(string? header)
    {
        using var response = await _api.GetAllRawAsync(header, "pageSize=2&page=1");

        await AssertValidationProblemAsync(response, "X-Session-Id", HeaderMessage);
    }

    [Fact]
    public async Task Next_UnknownSession_Returns404ProblemDetails()
    {
        var unknown = Guid.NewGuid();

        using var response = await _api.GetNextRawAsync(unknown.ToString());

        await AssertNotFoundAsync(response, unknown);
    }

    [Fact]
    public async Task All_UnknownSession_Returns404ProblemDetails()
    {
        var unknown = Guid.NewGuid();

        using var response = await _api.GetAllRawAsync(unknown.ToString(), "pageSize=2&page=1");

        await AssertNotFoundAsync(response, unknown);
    }

    [Fact]
    public async Task Next_EmptyGuid_Returns404()
    {
        using var response = await _api.GetNextRawAsync(Guid.Empty.ToString());

        await AssertNotFoundAsync(response, Guid.Empty);
    }

    [Theory]
    [InlineData("N")]
    [InlineData("B")]
    [InlineData("D")]
    public async Task Next_SessionIdInOtherGuidFormats_IsAccepted(string format)
    {
        var start = await _api.StartAsync(3);

        using var response = await _api.GetNextRawAsync(start.SessionId.ToString(format).ToUpperInvariant());

        var next = NextResult.From(await ApiClient.ReadJsonAsync(response, HttpStatusCode.OK));
        next.Index.Should().Be("1");
    }

    // ---- all: pageSize / page -------------------------------------------

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1001")]
    [InlineData("2147483647")]
    [InlineData("-2147483648")]
    public async Task All_PageSizeOutOfRange_Returns400ForPageSize(string pageSize)
    {
        var start = await _api.StartAsync(3);

        using var response = await _api.GetAllRawAsync(start.SessionId.ToString(), $"pageSize={pageSize}&page=1");

        await AssertValidationProblemAsync(response, "pageSize", "pageSize must be between 1 and 1000.");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("99999999999")]
    public async Task All_PageSizeNotAnInt32_Returns400ProblemForPageSize(string pageSize)
    {
        var start = await _api.StartAsync(3);

        using var response = await _api.GetAllRawAsync(start.SessionId.ToString(), $"pageSize={pageSize}&page=1");

        var json = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        json.GetProperty("errors").TryGetProperty("pageSize", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-9223372036854775808")]
    public async Task All_PageBelowOne_Returns400ForPage(string page)
    {
        var start = await _api.StartAsync(3);

        using var response = await _api.GetAllRawAsync(start.SessionId.ToString(), $"pageSize=2&page={page}");

        await AssertValidationProblemAsync(response, "page", "page must be greater than or equal to 1.");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("9223372036854775808")]
    [InlineData("1e3")]
    public async Task All_PageNotAnInt64_Returns400ProblemForPage(string page)
    {
        var start = await _api.StartAsync(3);

        using var response = await _api.GetAllRawAsync(start.SessionId.ToString(), $"pageSize=2&page={page}");

        var json = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        json.GetProperty("errors").TryGetProperty("page", out _).Should().BeTrue();
    }

    [Fact]
    public async Task All_PageBeyondLast_Returns400WithContractMessage()
    {
        var start = await _api.StartAsync(3);
        await _api.NextAsync(start.SessionId);
        await _api.NextAsync(start.SessionId);

        using var response = await _api.GetAllRawAsync(start.SessionId.ToString(), "pageSize=2&page=3");

        await AssertValidationProblemAsync(response, "page", "page must be between 1 and 2.");
    }

    [Fact]
    public async Task All_PageInt64MaxForN20_Returns400NotOverflow()
    {
        var start = await _api.StartAsync(20);

        using var response = await _api.GetAllRawAsync(
            start.SessionId.ToString(), $"pageSize=1&page={long.MaxValue}");

        await AssertValidationProblemAsync(response, "page", "page must be between 1 and 2432902008176640000.");
    }

    [Fact]
    public async Task All_Page2WhenNothingRemains_Returns400BetweenOneAndOne()
    {
        var start = await _api.StartAsync(1);
        await _api.NextAsync(start.SessionId);

        using var response = await _api.GetAllRawAsync(start.SessionId.ToString(), "pageSize=2&page=2");

        await AssertValidationProblemAsync(response, "page", "page must be between 1 and 1.");
    }

    [Fact]
    public async Task All_RejectedPage_DoesNotChangePositionOrCaptureAnchor()
    {
        var start = await _api.StartAsync(3);
        await _api.NextAsync(start.SessionId); // #1

        using (var rejected = await _api.GetAllRawAsync(start.SessionId.ToString(), "pageSize=2&page=99"))
        {
            rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        await _api.NextAsync(start.SessionId); // #2 – must continue from #1
        var page = await _api.AllAsync(start.SessionId, 2, 1);
        page.FirstIndex.Should().Be("3", "the rejected request must not have captured an anchor");
    }

    // ---- helpers ------------------------------------------------------------

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        var json = await ApiClient.ReadJsonAsync(response, status);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        json.GetProperty("status").GetInt32().Should().Be((int)status);
        json.TryGetProperty("title", out _).Should().BeTrue();
        json.TryGetProperty("type", out _).Should().BeTrue();
        json.TryGetProperty("traceId", out var traceId).Should().BeTrue("every error carries a traceId");
        traceId.GetString().Should().NotBeNullOrWhiteSpace();
        return json;
    }

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response, string field, string message)
    {
        var json = await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        json.GetProperty("type").GetString().Should().Be(BadRequestType);
        json.GetProperty("title").GetString().Should().Be(ValidationTitle);
        json.GetProperty("detail").GetString().Should().Be(message);
        var errors = json.GetProperty("errors");
        errors.EnumerateObject().Select(p => p.Name).Should().Equal(field);
        errors.GetProperty(field).EnumerateArray().Select(e => e.GetString()).Should().Equal(message);
    }

    private static async Task AssertNotFoundAsync(HttpResponseMessage response, Guid sessionId)
    {
        var json = await AssertProblemAsync(response, HttpStatusCode.NotFound);
        json.GetProperty("type").GetString().Should().Be(NotFoundType);
        json.GetProperty("title").GetString().Should().Be("Session not found");
        json.GetProperty("detail").GetString().Should().Be(
            $"Session '{sessionId}' was not found or has expired. Start a new session.");
    }
}
