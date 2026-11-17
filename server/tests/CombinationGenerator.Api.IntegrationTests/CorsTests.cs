using System.Net;
using CombinationGenerator.Api.IntegrationTests.Infrastructure;

namespace CombinationGenerator.Api.IntegrationTests;

/// <summary>Contract: only http://localhost:4200 is allowed, and it may send X-Session-Id.</summary>
public sealed class CorsTests : IClassFixture<ApiFactory>
{
    private const string AllowedOrigin = "http://localhost:4200";

    private readonly HttpClient _http;

    public CorsTests(ApiFactory factory)
    {
        _http = factory.CreateClient();
    }

    [Fact]
    public async Task Preflight_FromAngularOriginWithSessionHeader_IsAllowed()
    {
        using var request = Preflight(AllowedOrigin, "GET", "x-session-id");

        using var response = await _http.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be(AllowedOrigin);
        string.Join(',', response.Headers.GetValues("Access-Control-Allow-Headers"))
            .Should().ContainEquivalentOf("x-session-id");
    }

    [Fact]
    public async Task Preflight_ForStartWithContentType_IsAllowed()
    {
        using var request = Preflight(AllowedOrigin, "POST", "content-type");

        using var response = await _http.SendAsync(request);

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be(AllowedOrigin);
    }

    [Fact]
    public async Task Preflight_FromOtherOrigin_IsNotAllowed()
    {
        using var request = Preflight("http://evil.example", "GET", "x-session-id");

        using var response = await _http.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task SimpleRequest_FromAngularOrigin_CarriesAllowOriginHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiClient.BasePath}/start")
        {
            Content = JsonContent("""{ "n": 3 }"""),
        };
        request.Headers.Add("Origin", AllowedOrigin);

        using var response = await _http.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be(AllowedOrigin);
    }

    [Fact]
    public async Task ErrorResponse_FromAngularOrigin_CarriesAllowOriginHeader()
    {
        // Otherwise the browser hides the ProblemDetails body from the client.
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiClient.BasePath}/next");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add(ApiClient.SessionHeader, Guid.NewGuid().ToString());

        using var response = await _http.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeTrue();
    }

    [Fact]
    public async Task Preflight_ForBackWithSessionHeader_IsAllowed()
    {
        using var request = Preflight(AllowedOrigin, "POST", "x-session-id", $"{ApiClient.BasePath}/back");

        using var response = await _http.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be(AllowedOrigin);
        string.Join(',', response.Headers.GetValues("Access-Control-Allow-Headers"))
            .Should().ContainEquivalentOf("x-session-id");
        string.Join(',', response.Headers.GetValues("Access-Control-Allow-Methods"))
            .Should().ContainEquivalentOf("POST");
    }

    [Fact]
    public async Task Preflight_ForBackFromOtherOrigin_IsNotAllowed()
    {
        using var request = Preflight("http://evil.example", "POST", "x-session-id", $"{ApiClient.BasePath}/back");

        using var response = await _http.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Back_FromAngularOrigin_CarriesAllowOriginHeader()
    {
        var start = await new ApiClient(_http).StartAsync(3);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiClient.BasePath}/back");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add(ApiClient.SessionHeader, start.SessionId.ToString());

        using var response = await _http.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be(AllowedOrigin);
    }

    private static HttpRequestMessage Preflight(string origin, string method, string headers, string path)
    {
        var request = Preflight(origin, method, headers);
        request.RequestUri = new Uri(path, UriKind.Relative);
        return request;
    }

    private static HttpRequestMessage Preflight(string origin, string method, string headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, $"{ApiClient.BasePath}/next");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", headers);
        return request;
    }

    private static StringContent JsonContent(string json) =>
        new(json, System.Text.Encoding.UTF8, "application/json");
}
