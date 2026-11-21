using CombinationGenerator.Application.Dtos;
using CombinationGenerator.Application.Exceptions;

namespace CombinationGenerator.Application.Permutations;

/// <summary>
/// Use cases of the Combination Generator: start a session, step to the next permutation, page through
/// the permutations that follow the current one ("All" mode).
/// </summary>
public interface IPermutationService
{
    /// <summary>Creates a new, independent session positioned before permutation <c>#1</c>.</summary>
    /// <param name="n">Permutation size, <c>1..20</c>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new session id, <c>n</c> and <c>n!</c>.</returns>
    /// <exception cref="InvalidNException"><paramref name="n"/> is out of range.</exception>
    Task<StartSessionResponse> StartAsync(int n, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances the session by one permutation and ends "All" mode (the next page request captures a new anchor).
    /// When the session is already on the last permutation, returns a "No more combinations" response and leaves
    /// the position unchanged.
    /// </summary>
    /// <param name="sessionId">The session id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new current permutation, or the "No more combinations" response.</returns>
    /// <exception cref="SessionNotFoundException">The session is unknown or expired.</exception>
    Task<NextPermutationResponse> GetNextAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one page of the permutations that follow the paging anchor and moves the session's current position
    /// to the last item of the page ("Back" behavior). The anchor is captured on the first page request after
    /// <see cref="StartAsync"/>, <see cref="GetNextAsync"/> or <see cref="ExitAllModeAsync"/> and reused by every
    /// following page request.
    /// </summary>
    /// <param name="sessionId">The session id.</param>
    /// <param name="page">1-based page number; page 1 is always accepted.</param>
    /// <param name="pageSize">Items per page, <c>1..1000</c>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page (possibly empty with a "No more combinations" message).</returns>
    /// <exception cref="InvalidPageSizeException"><paramref name="pageSize"/> is out of range.</exception>
    /// <exception cref="InvalidPageException"><paramref name="page"/> is below 1 or beyond the last page.</exception>
    /// <exception cref="SessionNotFoundException">The session is unknown or expired.</exception>
    Task<PermutationPageResponse> GetAllAsync(
        Guid sessionId,
        long page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Leaves "All" mode ("Back"): clears the paging anchor without moving. The current position already is the last
    /// item of the last page served, which is what the client displays after Back; the next page request therefore
    /// captures a new anchor right after it, and <see cref="GetNextAsync"/> continues from it. Idempotent: calling it
    /// outside "All" mode changes nothing.
    /// </summary>
    /// <param name="sessionId">The session id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the session has left "All" mode.</returns>
    /// <exception cref="SessionNotFoundException">The session is unknown or expired.</exception>
    Task ExitAllModeAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
