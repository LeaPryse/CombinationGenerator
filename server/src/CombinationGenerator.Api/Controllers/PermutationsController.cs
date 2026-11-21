using CombinationGenerator.Api.Sessions;
using CombinationGenerator.Application.Dtos;
using CombinationGenerator.Application.Permutations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CombinationGenerator.Api.Controllers;

/// <summary>
/// HTTP endpoints of the permutation generator (see <c>docs/API_CONTRACT.md</c>). Thin: binds input, calls one
/// service method, returns its DTO. Errors are thrown as typed exceptions and mapped by
/// <see cref="ErrorHandling.ProblemDetailsExceptionHandler"/>.
/// </summary>
[ApiController]
[Route("api/permutations")]
public sealed class PermutationsController : ControllerBase
{
    private readonly IPermutationService _service;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="service">The use-case service.</param>
    public PermutationsController(IPermutationService service)
    {
        _service = service;
    }

    /// <summary>Creates a new session for permutations of 1..n (also used by the client to Reset).</summary>
    /// <param name="request">The requested <c>n</c> (1..20).</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The session id, <c>n</c> and <c>n!</c> (as a string).</returns>
    /// <remarks>
    /// An empty body is allowed through model binding (<see cref="EmptyBodyBehavior.Allow"/>) so that a request with
    /// no body at all (and therefore no Content-Type) yields the contract's 400 "n must be between 1 and 20." instead
    /// of the framework's 415. A non-empty body with a non-JSON Content-Type is still rejected with 415. A body that
    /// is not valid JSON or whose <c>n</c> is not an integer is turned into the same <c>n</c> error by
    /// <see cref="ErrorHandling.InvalidModelStateResponse"/>. Returns 503 when the server's session limit is reached.
    /// </remarks>
    [HttpPost("start")]
    [ProducesResponseType<StartSessionResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<StartSessionResponse>> StartAPI(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] StartSessionRequest? request,
        CancellationToken cancellationToken)
    {
        // A missing body means n was not supplied; 0 is out of range, so the service reports the standard n error.
        return Ok(await _service.StartAsync(request?.N ?? 0, cancellationToken));
    }

    /// <summary>Advances the session by one permutation and ends "All" mode.</summary>
    /// <param name="sessionId">The <c>X-Session-Id</c> header (GUID returned by <c>start</c>).</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The next permutation, or <c>"No more combinations"</c>.</returns>
    [HttpGet("next")]
    [ProducesResponseType<NextPermutationResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<NextPermutationResponse>> GetNextAPI(
        [FromHeader(Name = SessionHeader.Name)] string? sessionId,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetNextAsync(SessionHeader.Parse(sessionId), cancellationToken));
    }

    /// <summary>
    /// Returns one page of the permutations after the paging anchor (the permutation displayed when "All" mode was
    /// entered) and moves the current position to the last item of the page.
    /// </summary>
    /// <param name="sessionId">The <c>X-Session-Id</c> header (GUID returned by <c>start</c>).</param>
    /// <param name="pageSize">Items per page, 1..1000 (default 50).</param>
    /// <param name="page">1-based page number, up to <c>totalPages</c> (default 1; page 1 is always accepted).</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The page.</returns>
    [HttpGet("all")]
    [ProducesResponseType<PermutationPageResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PermutationPageResponse>> GetAllAPI(
        [FromHeader(Name = SessionHeader.Name)] string? sessionId,
        [FromQuery] int pageSize = PagingDefaults.PageSize,
        [FromQuery] long page = PagingDefaults.Page,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetAllAsync(SessionHeader.Parse(sessionId), page, pageSize, cancellationToken));
    }

    /// <summary>
    /// Leaves "All" mode ("Back"): clears the paging anchor without moving the current position (which already is the
    /// last item of the last page served). The next <c>all</c> starts right after that item. Idempotent.
    /// </summary>
    /// <param name="sessionId">The <c>X-Session-Id</c> header (GUID returned by <c>start</c>).</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns><c>204 No Content</c>.</returns>
    [HttpPost("back")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Back(
        [FromHeader(Name = SessionHeader.Name)] string? sessionId,
        CancellationToken cancellationToken)
    {
        await _service.ExitAllModeAsync(SessionHeader.Parse(sessionId), cancellationToken);
        return NoContent();
    }
}
