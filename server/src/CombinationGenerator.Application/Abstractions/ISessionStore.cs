using CombinationGenerator.Application.Exceptions;
using CombinationGenerator.Domain.Sessions;

namespace CombinationGenerator.Application.Abstractions;

/// <summary>
/// Persistence of <see cref="PermutationSession"/> instances (the "DAL" of this application).
/// </summary>
/// <remarks>
/// The abstraction is asynchronous and cancellable even though the in-memory implementation is synchronous,
/// so that a distributed implementation (e.g. Redis) needs no signature change.
/// Implementations apply a sliding expiration: every <see cref="ExecuteAsync{TResult}"/> that finds the session
/// resets its timer, even if the operation then throws.
/// </remarks>
public interface ISessionStore
{
    /// <summary>Stores a newly created session.</summary>
    /// <param name="session">The session to store.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the session is stored.</returns>
    /// <exception cref="SessionCapacityExceededException">
    /// The store is full (bounded implementations only); no existing session is evicted to make room.
    /// </exception>
    Task AddAsync(PermutationSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the session, runs <paramref name="operation"/> on it while holding an exclusive per-session lock,
    /// and persists the resulting state. Concurrent calls for the same session are serialized; calls for different
    /// sessions do not block each other.
    /// </summary>
    /// <remarks>
    /// The operation must be atomic with respect to the session (either it fully succeeds or it leaves the session
    /// unchanged and throws): the <see cref="PermutationSession"/> methods guarantee this. Exceptions thrown by the
    /// operation propagate to the caller.
    /// </remarks>
    /// <typeparam name="TResult">Result type of the operation.</typeparam>
    /// <param name="sessionId">Identifier of the session.</param>
    /// <param name="operation">The mutation/query to execute on the session.</param>
    /// <param name="cancellationToken">Cancels waiting for the session lock.</param>
    /// <returns>The value returned by <paramref name="operation"/>.</returns>
    /// <exception cref="SessionNotFoundException">No session with that id exists (unknown or expired).</exception>
    Task<TResult> ExecuteAsync<TResult>(
        Guid sessionId,
        Func<PermutationSession, TResult> operation,
        CancellationToken cancellationToken = default);
}
