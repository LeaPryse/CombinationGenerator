using System.Net;
using CombinationGenerator.Api.IntegrationTests.Infrastructure;

namespace CombinationGenerator.Api.IntegrationTests;

/// <summary>Contract: sliding expiration of 30 minutes; an expired session returns 404 like an unknown one.</summary>
public sealed class SessionExpirationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly ApiClient _api;

    public SessionExpirationTests(ApiFactory factory)
    {
        _factory = factory;
        _api = new ApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task Next_After31IdleMinutes_Returns404()
    {
        var id = (await _api.StartAsync(3)).SessionId;

        _factory.Clock.Advance(TimeSpan.FromMinutes(31));
        using var response = await _api.GetNextRawAsync(id.ToString());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task All_After31IdleMinutes_Returns404()
    {
        var id = (await _api.StartAsync(3)).SessionId;

        _factory.Clock.Advance(TimeSpan.FromMinutes(31));
        using var response = await _api.GetAllRawAsync(id.ToString(), "pageSize=2&page=1");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Next_After29IdleMinutes_StillWorks()
    {
        var id = (await _api.StartAsync(3)).SessionId;

        _factory.Clock.Advance(TimeSpan.FromMinutes(29));
        var next = await _api.NextAsync(id);

        next.Index.Should().Be("1");
    }

    [Fact]
    public async Task Activity_ResetsSlidingTimer()
    {
        var id = (await _api.StartAsync(3)).SessionId;

        for (var i = 1; i <= 4; i++)
        {
            _factory.Clock.Advance(TimeSpan.FromMinutes(20)); // 80 minutes total, never 30 idle
            (await _api.NextAsync(id)).Index.Should().Be(i.ToString());
        }

        _factory.Clock.Advance(TimeSpan.FromMinutes(20));
        (await _api.AllAsync(id, 1, 1)).Items.Single().Index.Should().Be("5");
    }
}
