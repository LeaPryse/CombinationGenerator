using System.Net;
using CombinationGenerator.Api.IntegrationTests.Infrastructure;
using CombinationGenerator.Application.Dtos;
using CombinationGenerator.Application.Permutations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CombinationGenerator.Api.IntegrationTests;

/// <summary>Unexpected failures (500) and logging hygiene of expected client errors.</summary>
public sealed class DiagnosticsTests
{
    private const string SecretMessage = "secret-internal-detail-42";

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task UnexpectedException_Returns500ProblemDetailsWithoutInternalDetails(string environment)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureServices(services => services.AddSingleton<IPermutationService, ThrowingService>());
        });
        var api = new ApiClient(factory.CreateClient());

        using var response = await api.GetNextRawAsync(Guid.NewGuid().ToString());

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        body.Should().NotContain(SecretMessage);
        body.Should().NotContain("InvalidOperationException");
        body.Should().NotContain(" at ");
        body.Should().Contain("traceId");
    }

    [Fact]
    public async Task ExpectedClientErrors_AreNotLoggedAsErrors()
    {
        var logs = new CapturingLoggerProvider();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        var api = new ApiClient(factory.CreateClient());

        using (await api.GetNextRawAsync(Guid.NewGuid().ToString()))
        using (await api.GetNextRawAsync("not-a-guid"))
        using (await api.PostStartRawAsync("""{ "n": 21 }"""))
        {
        }

        // 400/404 are normal client outcomes (expired sessions are routine). Logging each one at Error level with a
        // full stack trace floods the logs and trips error-based alerting.
        logs.Entries.Where(e => e.Level >= LogLevel.Error)
            .Select(e => $"{e.Category}: {e.Message} ({e.Exception?.GetType().Name})")
            .Should().BeEmpty();
    }

    private sealed class ThrowingService : IPermutationService
    {
        public Task<StartSessionResponse> StartAsync(int n, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(SecretMessage);

        public Task<NextPermutationResponse> GetNextAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(SecretMessage);

        public Task<PermutationPageResponse> GetAllAsync(
            Guid sessionId, long page, int pageSize, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(SecretMessage);

        public Task ExitAllModeAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(SecretMessage);
    }
}
