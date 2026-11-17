using CombinationGenerator.Application.Permutations;
using Microsoft.Extensions.DependencyInjection;

namespace CombinationGenerator.Application;

/// <summary>Registers the Application layer services.</summary>
public static class DependencyInjection
{
    /// <summary>Adds the use-case services. Requires an <c>ISessionStore</c> registration (see Infrastructure).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Stateless: all state lives in ISessionStore, so a singleton is safe.
        services.AddSingleton<IPermutationService, PermutationService>();
        return services;
    }
}
