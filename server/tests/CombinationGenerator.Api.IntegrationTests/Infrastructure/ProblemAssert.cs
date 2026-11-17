using System.Net;
using System.Text.Json;

namespace CombinationGenerator.Api.IntegrationTests.Infrastructure;

/// <summary>Shared assertions for RFC 7807 ProblemDetails responses (docs/API_CONTRACT.md, "Errors").</summary>
public static class ProblemAssert
{
    public const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    public const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    public const string ServiceUnavailableType = "https://tools.ietf.org/html/rfc9110#section-15.6.4";
    public const string ValidationTitle = "One or more validation errors occurred.";
    public const string HeaderMessage = "The X-Session-Id header is required and must be a GUID.";
    public const string NMessage = "n must be between 1 and 20.";
    public const string PageSizeMessage = "pageSize must be between 1 and 1000.";
    public const string PageNotIntegerMessage = "page must be an integer greater than or equal to 1.";
    public const string PageTooSmallMessage = "page must be greater than or equal to 1.";

    public static async Task<JsonElement> ProblemAsync(HttpResponseMessage response, HttpStatusCode status)
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

    /// <summary>A 400 whose <c>errors</c> has exactly one key, <paramref name="field"/>, with exactly <paramref name="message"/>.</summary>
    public static async Task ValidationAsync(HttpResponseMessage response, string field, string message)
    {
        await ValidationAsync(response, (field, message));
    }

    /// <summary>
    /// A 400 whose <c>errors</c> has exactly the given keys (any order), each with exactly its message, and whose
    /// <c>detail</c> repeats one of them (the first reported).
    /// </summary>
    public static async Task ValidationAsync(HttpResponseMessage response, params (string Field, string Message)[] expected)
    {
        var json = await ProblemAsync(response, HttpStatusCode.BadRequest);
        json.GetProperty("type").GetString().Should().Be(BadRequestType);
        json.GetProperty("title").GetString().Should().Be(ValidationTitle);

        var errors = json.GetProperty("errors");
        errors.ValueKind.Should().Be(JsonValueKind.Object);
        errors.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(expected.Select(e => e.Field));
        foreach (var (field, message) in expected)
        {
            errors.GetProperty(field).EnumerateArray().Select(e => e.GetString()).Should().Equal(
                new[] { message }, $"errors.{field} must carry exactly the contract message");
        }

        var firstReported = errors.EnumerateObject().First().Value.EnumerateArray().First().GetString();
        json.GetProperty("detail").GetString().Should().Be(firstReported, "detail repeats the first message");
    }

    public static async Task NotFoundAsync(HttpResponseMessage response, Guid sessionId)
    {
        var json = await ProblemAsync(response, HttpStatusCode.NotFound);
        json.GetProperty("type").GetString().Should().Be(NotFoundType);
        json.GetProperty("title").GetString().Should().Be("Session not found");
        json.GetProperty("detail").GetString().Should().Be(
            $"Session '{sessionId}' was not found or has expired. Start a new session.");
    }
}
