using System.Net;
using System.Net.Http.Json;
using CombinationGenerator.Api.IntegrationTests.Infrastructure;

namespace CombinationGenerator.Api.IntegrationTests;

/// <summary>
/// Contract "Session" + "503 – session limit reached": at most Sessions:MaxSessions live sessions; live sessions are
/// never evicted; expired sessions are purged before start is rejected.
/// </summary>
public sealed class SessionLimitTests : IDisposable
{
    private const string LimitDetail = "The server has reached its maximum number of active sessions. Try again later.";

    private readonly LimitedSessionsApiFactory _factory = new(maxSessions: 2);
    private readonly ApiClient _api;

    public SessionLimitTests()
    {
        _api = new ApiClient(_factory.CreateClient());
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Start_BeyondLimit_Returns503ProblemDetails()
    {
        await _api.StartAsync(3);
        await _api.StartAsync(4);

        using var response = await PostStartAsync(5);

        await AssertLimitReachedAsync(response);
    }

    [Fact]
    public async Task Start_BeyondLimit_ExistingSessionsKeepWorkingAndAreNotEvicted()
    {
        var a = (await _api.StartAsync(3)).SessionId;
        var b = (await _api.StartAsync(4)).SessionId;
        await _api.NextAsync(a); // a at #1

        for (var i = 0; i < 5; i++)
        {
            using var rejected = await PostStartAsync(5);
            rejected.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        }

        (await _api.NextAsync(a)).Index.Should().Be("2", "a keeps its state");
        (await _api.NextAsync(b)).Index.Should().Be("1");
        (await _api.AllAsync(b, 2, 1)).FirstIndex.Should().Be("2");
        await _api.BackAsync(a);
    }

    [Fact]
    public async Task Start_InvalidNWhileFull_StillReturns400NotA503()
    {
        await _api.StartAsync(3);
        await _api.StartAsync(3);

        using var response = await _api.PostStartRawAsync("""{ "n": 21 }""");

        await ProblemAssert.ValidationAsync(response, "n", ProblemAssert.NMessage);
    }

    [Fact]
    public async Task Start_AfterAllSessionsExpire_CapacityFreesUp()
    {
        var a = (await _api.StartAsync(3)).SessionId;
        await _api.StartAsync(3);
        using (var rejected = await PostStartAsync(3))
        {
            rejected.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        }

        _factory.Clock.Advance(TimeSpan.FromMinutes(31));

        var c = (await _api.StartAsync(3)).SessionId;
        var d = (await _api.StartAsync(3)).SessionId;
        (await _api.NextAsync(c)).Index.Should().Be("1");
        (await _api.NextAsync(d)).Index.Should().Be("1");
        using (var expired = await _api.GetNextRawAsync(a.ToString()))
        {
            await ProblemAssert.NotFoundAsync(expired, a);
        }

        using var full = await PostStartAsync(3);
        await AssertLimitReachedAsync(full);
    }

    [Fact]
    public async Task Start_WhenOnlyIdleSessionExpired_ReplacesItAndKeepsActiveOne()
    {
        var active = (await _api.StartAsync(3)).SessionId;
        var idle = (await _api.StartAsync(3)).SessionId;

        _factory.Clock.Advance(TimeSpan.FromMinutes(20));
        await _api.NextAsync(active);                         // active refreshed, idle not
        _factory.Clock.Advance(TimeSpan.FromMinutes(15));     // idle: 35 min, active: 15 min

        var fresh = (await _api.StartAsync(3)).SessionId;     // room from the expired idle session

        using (var rejected = await PostStartAsync(3))
        {
            await AssertLimitReachedAsync(rejected);           // active + fresh = full
        }

        (await _api.NextAsync(active)).Index.Should().Be("2", "the live session must never be evicted");
        (await _api.NextAsync(fresh)).Index.Should().Be("1");
        using var gone = await _api.GetNextRawAsync(idle.ToString());
        await ProblemAssert.NotFoundAsync(gone, idle);
    }

    [Fact]
    public async Task Start_ParallelBurst_NeverExceedsLimit()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => PostStartAsync(3)));
        try
        {
            responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(2);
            responses.Count(r => r.StatusCode == HttpStatusCode.ServiceUnavailable).Should().Be(18);
        }
        finally
        {
            foreach (var r in responses)
            {
                r.Dispose();
            }
        }
    }

    private Task<HttpResponseMessage> PostStartAsync(int n) =>
        _api.Http.PostAsJsonAsync($"{ApiClient.BasePath}/start", new { n });

    private static async Task AssertLimitReachedAsync(HttpResponseMessage response)
    {
        var json = await ProblemAssert.ProblemAsync(response, HttpStatusCode.ServiceUnavailable);
        json.GetProperty("type").GetString().Should().Be(ProblemAssert.ServiceUnavailableType);
        json.GetProperty("title").GetString().Should().Be("Session limit reached");
        json.GetProperty("detail").GetString().Should().Be(LimitDetail);
        json.TryGetProperty("errors", out _).Should().BeFalse("503 is not a validation problem");
    }
}
