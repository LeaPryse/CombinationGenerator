namespace CombinationGenerator.Application.Exceptions;

/// <summary>
/// Thrown by <see cref="Abstractions.ISessionStore.AddAsync"/> when the store already holds its configured maximum
/// number of live sessions and cannot accept a new one. Existing sessions are never evicted to make room.
/// The Api maps it to <c>503 Service Unavailable</c>.
/// </summary>
public sealed class SessionCapacityExceededException : Exception
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="maxSessions">The configured maximum number of live sessions.</param>
    public SessionCapacityExceededException(long maxSessions)
        : base("The server has reached its maximum number of active sessions. Try again later.")
    {
        MaxSessions = maxSessions;
    }

    /// <summary>The configured maximum number of live sessions.</summary>
    public long MaxSessions { get; }
}
