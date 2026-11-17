using CombinationGenerator.Infrastructure.Sessions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace CombinationGenerator.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Like <see cref="ApiFactory"/> (controllable clock) but with a small <c>Sessions:MaxSessions</c>, so the 503
/// "Session limit reached" path can be exercised. Create one per test: the capacity is shared state.
/// </summary>
public sealed class LimitedSessionsApiFactory : WebApplicationFactory<Program>
{
    public LimitedSessionsApiFactory(int maxSessions)
    {
        MaxSessions = maxSessions;
    }

    public int MaxSessions { get; }

    public FakeClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.Configure<MemoryCacheOptions>(options =>
            {
#pragma warning disable CS0618 // ISystemClock is the only clock hook of MemoryCache 8.x.
                options.Clock = Clock;
#pragma warning restore CS0618
            });
            services.PostConfigure<SessionOptions>(options => options.MaxSessions = MaxSessions);
        });
    }
}
