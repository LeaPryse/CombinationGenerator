using CombinationGenerator.Application.Exceptions;
using CombinationGenerator.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CombinationGenerator.Api.ErrorHandling;

/// <summary>
/// The single place where exceptions become HTTP responses: RFC 7807 ProblemDetails
/// (<c>application/problem+json</c>, with a <c>traceId</c> extension added by <see cref="IProblemDetailsService"/>).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="RequestValidationException"/> → 400 with <c>errors: { field: [message], … }</c>, one entry per
/// offending field (<see cref="RequestValidationException.Errors"/>).</item>
/// <item><see cref="DomainException"/> (should not escape the Application layer) → 400.</item>
/// <item><see cref="SessionNotFoundException"/> → 404.</item>
/// <item><see cref="SessionCapacityExceededException"/> → 503 (the session limit is reached; retry later).</item>
/// <item>Anything else → 500 without internal details.</item>
/// </list>
/// This handler is the only place exceptions are logged: expected client errors at Information/Warning, unexpected
/// ones at Error. The framework's own <c>ExceptionHandlerMiddleware</c> log (which would record every 400/404 at
/// Error with a stack trace) is silenced in <c>appsettings.json</c>.
/// </remarks>
public sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
    private const string ValidationTitle = "One or more validation errors occurred.";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string ServerErrorType = "https://tools.ietf.org/html/rfc9110#section-15.6.1";
    private const string ServiceUnavailableType = "https://tools.ietf.org/html/rfc9110#section-15.6.4";

    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<ProblemDetailsExceptionHandler> _logger;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="problemDetailsService">Writes ProblemDetails responses.</param>
    /// <param name="logger">The logger.</param>
    public ProblemDetailsExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<ProblemDetailsExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        switch (exception)
        {
            case RequestValidationException validation:
                _logger.LogInformation(
                    "Validation failed for {Fields}: {Message}",
                    string.Join(", ", validation.Errors.Select(e => e.Key)),
                    validation.Message);
                problem = Validation(validation);
                break;

            case DomainException domain:
                _logger.LogWarning(domain, "Domain rule violated: {Message}", domain.Message);
                problem = new ProblemDetails
                {
                    Type = BadRequestType,
                    Title = "The request violates a domain rule.",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = domain.Message,
                };
                break;

            case SessionNotFoundException notFound:
                _logger.LogInformation("Session {SessionId} not found or expired", notFound.SessionId);
                problem = new ProblemDetails
                {
                    Type = NotFoundType,
                    Title = "Session not found",
                    Status = StatusCodes.Status404NotFound,
                    Detail = notFound.Message,
                };
                break;

            case SessionCapacityExceededException capacity:
                _logger.LogWarning(
                    "Session limit of {MaxSessions} reached; rejecting new session",
                    capacity.MaxSessions);
                problem = new ProblemDetails
                {
                    Type = ServiceUnavailableType,
                    Title = "Session limit reached",
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Detail = capacity.Message,
                };
                break;

            default:
                _logger.LogError(exception, "Unhandled exception while processing {Path}", httpContext.Request.Path);
                problem = new ProblemDetails
                {
                    Type = ServerErrorType,
                    Title = "An unexpected error occurred.",
                    Status = StatusCodes.Status500InternalServerError,
                };
                break;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    /// <summary>
    /// One <c>errors</c> entry per offending field, in the exception's order; <c>detail</c> is the first message.
    /// </summary>
    private static ValidationProblemDetails Validation(RequestValidationException validation)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (field, message) in validation.Errors)
        {
            errors[field] = errors.TryGetValue(field, out var existing) ? [.. existing, message] : [message];
        }

        return new ValidationProblemDetails(errors)
        {
            Type = BadRequestType,
            Title = ValidationTitle,
            Status = StatusCodes.Status400BadRequest,
            Detail = validation.Message,
        };
    }
}
