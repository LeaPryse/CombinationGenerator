namespace CombinationGenerator.Infrastructure.Sessions;

/// <summary>Configuration of session storage (configuration section <see cref="SectionName"/>).</summary>
public sealed class SessionOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Sessions";

    /// <summary>Default sliding expiration, as required by the API contract.</summary>
    public const int DefaultSlidingExpirationMinutes = 30;

    /// <summary>Default maximum number of live sessions.</summary>
    public const int DefaultMaxSessions = 10_000;

    /// <summary>
    /// Minutes of inactivity after which a session expires (must be &gt; 0). Every call that finds the session resets
    /// the timer.
    /// </summary>
    public int SlidingExpirationMinutes { get; set; } = DefaultSlidingExpirationMinutes;

    /// <summary>
    /// Maximum number of live (not yet expired) sessions held in memory (must be &gt; 0). When the limit is reached,
    /// <c>start</c> is rejected (503) instead of evicting an active session. A session is O(n) ≈ a few hundred bytes,
    /// so the default bounds session memory to a few MB.
    /// </summary>
    public int MaxSessions { get; set; } = DefaultMaxSessions;
}
