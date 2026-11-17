using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace CombinationGenerator.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in memory. The memory cache uses a controllable clock so that session expiration can be tested
/// without waiting 30 minutes; production wiring (store, service, controllers, error handling) is unchanged.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public FakeClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
            services.Configure<MemoryCacheOptions>(options =>
            {
#pragma warning disable CS0618 // ISystemClock is the only clock hook of MemoryCache 8.x.
                options.Clock = Clock;
#pragma warning restore CS0618
            }));
    }
}
