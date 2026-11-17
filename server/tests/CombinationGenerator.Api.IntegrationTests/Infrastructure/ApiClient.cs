using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace CombinationGenerator.Api.IntegrationTests.Infrastructure;

/// <summary>Thin typed wrapper over the HTTP API. Parses responses as raw JSON so the wire format itself is asserted.</summary>
public sealed class ApiClient
{
    public const string SessionHeader = "X-Session-Id";
    public const string BasePath = "/api/permutations";

    public ApiClient(HttpClient http)
    {
        Http = http;
    }

    public HttpClient Http { get; }

    public Task<HttpResponseMessage> PostStartRawAsync(string? jsonBody, string mediaType = "application/json")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BasePath}/start");
        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, mediaType);
        }

        return Http.SendAsync(request);
    }

    public async Task<StartResult> StartAsync(int n)
    {
        using var response = await Http.PostAsJsonAsync($"{BasePath}/start", new { n });
        var json = await ReadJsonAsync(response, HttpStatusCode.OK);
        return new StartResult(
            Guid.Parse(json.GetProperty("sessionId").GetString()!),
            json.GetProperty("n").GetInt32(),
            json.GetProperty("totalCount").GetString()!);
    }

    public Task<HttpResponseMessage> GetNextRawAsync(string? sessionHeader)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{BasePath}/next");
        if (sessionHeader is not null)
        {
            request.Headers.TryAddWithoutValidation(SessionHeader, sessionHeader);
        }

        return Http.SendAsync(request);
    }

    public async Task<NextResult> NextAsync(Guid sessionId)
    {
        using var response = await GetNextRawAsync(sessionId.ToString());
        var json = await ReadJsonAsync(response, HttpStatusCode.OK);
        return NextResult.From(json);
    }

    public Task<HttpResponseMessage> GetAllRawAsync(string? sessionHeader, string? query)
    {
        var url = $"{BasePath}/all" + (query is null ? string.Empty : "?" + query);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (sessionHeader is not null)
        {
            request.Headers.TryAddWithoutValidation(SessionHeader, sessionHeader);
        }

        return Http.SendAsync(request);
    }

    public async Task<PageResult> AllAsync(Guid sessionId, int pageSize, long page)
    {
        using var response = await GetAllRawAsync(sessionId.ToString(), $"pageSize={pageSize}&page={page}");
        var json = await ReadJsonAsync(response, HttpStatusCode.OK);
        return PageResult.From(json);
    }

    public Task<HttpResponseMessage> PostBackRawAsync(string? sessionHeader)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BasePath}/back");
        if (sessionHeader is not null)
        {
            request.Headers.TryAddWithoutValidation(SessionHeader, sessionHeader);
        }

        return Http.SendAsync(request);
    }

    /// <summary>Calls <c>POST /back</c> and asserts the contract's <c>204 No Content</c> with an empty body.</summary>
    public async Task BackAsync(Guid sessionId)
    {
        using var response = await PostBackRawAsync(sessionId.ToString());
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, "body was: {0}", body);
        body.Should().BeEmpty("204 has no body");
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected, "body was: {0}", body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}

public sealed record StartResult(Guid SessionId, int N, string TotalCount);

public sealed record NextResult(string? Index, int[]? Permutation, bool HasMore, string? Message)
{
    public static NextResult From(JsonElement json) => new(
        Json.StringOrNull(json, "index"),
        Json.IntArrayOrNull(json, "permutation"),
        json.GetProperty("hasMore").GetBoolean(),
        Json.StringOrNull(json, "message"));
}

public sealed record PageItem(string Index, int[] Permutation);

public sealed record PageResult(
    IReadOnlyList<PageItem> Items,
    string Page,
    int PageSize,
    string TotalPages,
    string TotalItems,
    string? FirstIndex,
    string? Message)
{
    public static PageResult From(JsonElement json) => new(
        json.GetProperty("items").EnumerateArray()
            .Select(i => new PageItem(
                Json.RequireString(i, "index"),
                Json.IntArrayOrNull(i, "permutation")!))
            .ToList(),
        Json.RequireString(json, "page"),
        json.GetProperty("pageSize").GetInt32(),
        Json.RequireString(json, "totalPages"),
        Json.RequireString(json, "totalItems"),
        Json.StringOrNull(json, "firstIndex"),
        Json.StringOrNull(json, "message"));
}

/// <summary>Strict JSON readers: a property must be present (contract: never omitted) and of the documented kind.</summary>
public static class Json
{
    public static string RequireString(JsonElement obj, string name)
    {
        obj.TryGetProperty(name, out var value).Should().BeTrue($"property '{name}' must be present");
        value.ValueKind.Should().Be(JsonValueKind.String, $"'{name}' must be a JSON string");
        return value.GetString()!;
    }

    public static string? StringOrNull(JsonElement obj, string name)
    {
        obj.TryGetProperty(name, out var value).Should().BeTrue($"property '{name}' must be present (null, not omitted)");
        value.ValueKind.Should().BeOneOf(new[] { JsonValueKind.String, JsonValueKind.Null }, $"'{name}' must be string or null");
        return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
    }

    public static int[]? IntArrayOrNull(JsonElement obj, string name)
    {
        obj.TryGetProperty(name, out var value).Should().BeTrue($"property '{name}' must be present (null, not omitted)");
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        value.ValueKind.Should().Be(JsonValueKind.Array);
        return value.EnumerateArray().Select(e =>
        {
            e.ValueKind.Should().Be(JsonValueKind.Number, $"elements of '{name}' must be JSON numbers");
            return e.GetInt32();
        }).ToArray();
    }
}
