using CombinationGenerator.Application.Abstractions;
using CombinationGenerator.Infrastructure.Sessions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CombinationGenerator.Infrastructure;

/// <summary>Registers the Infrastructure layer services.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds the in-memory <see cref="ISessionStore"/> and binds <see cref="SessionOptions"/> from the
    /// <see cref="SessionOptions.SectionName"/> configuration section (validated at startup).
    /// </summary>
    /// <remarks>
    /// The store keeps its own session registry with deterministic sliding expiration and capacity accounting (see
    /// <see cref="InMemorySessionStore"/>); it does not use the shared <c>IMemoryCache</c>, only the clock and scan
    /// frequency of <c>MemoryCacheOptions</c>.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<SessionOptions>()
            .Bind(configuration.GetSection(SessionOptions.SectionName))
            .Validate(
                o => o.SlidingExpirationMinutes > 0,
                $"{SessionOptions.SectionName}:{nameof(SessionOptions.SlidingExpirationMinutes)} must be greater than 0.")
            .Validate(
                o => o.MaxSessions > 0,
                $"{SessionOptions.SectionName}:{nameof(SessionOptions.MaxSessions)} must be greater than 0.")
            .ValidateOnStart();

        services.AddSingleton<ISessionStore, InMemorySessionStore>();
        return services;
    }
}
