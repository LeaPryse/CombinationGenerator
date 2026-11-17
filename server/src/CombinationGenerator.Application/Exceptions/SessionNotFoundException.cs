namespace CombinationGenerator.Application.Exceptions;

/// <summary>
/// Thrown when a session id is unknown or the session has expired (both are indistinguishable for the client).
/// The Api maps it to <c>404</c>.
/// </summary>
public sealed class SessionNotFoundException : Exception
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="sessionId">The id that was not found.</param>
    public SessionNotFoundException(Guid sessionId)
        : base($"Session '{sessionId}' was not found or has expired. Start a new session.")
    {
        SessionId = sessionId;
    }

    /// <summary>The id that was not found.</summary>
    public Guid SessionId { get; }
}
