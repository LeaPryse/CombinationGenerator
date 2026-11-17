using System.Collections.Concurrent;
using CombinationGenerator.Application.Abstractions;
using CombinationGenerator.Application.Exceptions;
using CombinationGenerator.Domain.Sessions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CombinationGenerator.Infrastructure.Sessions;

/// <summary>
/// In-memory <see cref="ISessionStore"/> with sliding expiration and a hard capacity limit. Each entry holds the
/// session together with its own <see cref="SemaphoreSlim"/>, so mutations of one session are serialized while
/// different sessions proceed in parallel.
/// </summary>
/// <remarks>
/// <para><b>Why not <see cref="MemoryCache"/>.</b> An earlier version used a size-limited <see cref="MemoryCache"/>.
/// Its capacity accounting is updated by background work (expiration scans, over-capacity compaction) that races a
/// synchronous <c>Compact</c>, so a <c>start</c> could be rejected although an expired session existed. This store keeps
/// its own registry instead, so expiration and capacity are decided synchronously and deterministically.</para>
/// <para><b>Expiration.</b> Every entry records its last access time (ticks of the clock). An entry is expired when
/// <c>now - lastAccess &gt;= SlidingExpiration</c> (same rule as <see cref="MemoryCache"/>). Every
/// <see cref="ExecuteAsync{TResult}"/> that finds a live session refreshes its last access time before running the
/// operation, including calls whose operation then throws (e.g. page out of range). An expired entry is never
/// revived: a lookup that finds it removes it and reports <see cref="SessionNotFoundException"/>.</para>
/// <para><b>Atomicity.</b> "Check expired, then refresh" and "check expired, then remove" are each a single
/// compare-and-swap on the entry's last-access field; removal first swaps in a <i>dead</i> marker. Hence a session is
/// either refreshed (and kept) or declared dead (and gone), never both, even if a lookup and a purge race.</para>
/// <para><b>Capacity.</b> <see cref="AddAsync"/> runs under a store-wide lock (adds only; lookups are lock-free).
/// When <see cref="SessionOptions.MaxSessions"/> live entries are registered it first purges every expired entry,
/// synchronously; only if the store is still full does it throw <see cref="SessionCapacityExceededException"/>
/// (mapped to 503). Live sessions are never evicted. Since removals only ever lower the count and inserts are
/// serialized, the count can never exceed the limit.</para>
/// <para><b>Housekeeping.</b> Expired entries are also purged opportunistically, at most once per
/// <see cref="MemoryCacheOptions.ExpirationScanFrequency"/>, when the store is used, so memory of abandoned sessions is
/// released even while the store is not full.</para>
/// <para>The store reuses the clock and scan frequency of the application's <see cref="MemoryCacheOptions"/>, so a test
/// clock configured there also drives session expiration.</para>
/// <para>The session object is mutated in place, so "persisting" after the operation is implicit.
/// The semaphores are never disposed: <see cref="SemaphoreSlim"/> only holds unmanaged resources when
/// <see cref="SemaphoreSlim.AvailableWaitHandle"/> is accessed (it is not), and disposing on removal could race with
/// a request that already obtained the entry. A removed entry is simply garbage-collected.</para>
/// </remarks>
public sealed class InMemorySessionStore : ISessionStore, IDisposable
{
    private readonly ConcurrentDictionary<Guid, SessionEntry> _sessions = new();
    private readonly object _addLock = new();
    private readonly long _slidingTicks;
    private readonly long _scanFrequencyTicks;
    private readonly int _maxSessions;
#pragma warning disable CS0618 // ISystemClock is the only clock hook of MemoryCacheOptions 8.x (used by tests).
    private readonly Microsoft.Extensions.Internal.ISystemClock? _clock;
#pragma warning restore CS0618
    private readonly ILogger<InMemorySessionStore> _logger;
    private long _lastScanTicks;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="options">Session options (sliding expiration, maximum number of sessions).</param>
    /// <param name="cacheDefaults">Application-wide cache options; only the clock and scan frequency are reused.</param>
    /// <param name="loggerFactory">Logger factory.</param>
    public InMemorySessionStore(
        IOptions<SessionOptions> options,
        IOptions<MemoryCacheOptions> cacheDefaults,
        ILoggerFactory loggerFactory)
    {
        _slidingTicks = TimeSpan.FromMinutes(options.Value.SlidingExpirationMinutes).Ticks;
        _maxSessions = options.Value.MaxSessions;

        var defaults = cacheDefaults.Value;
#pragma warning disable CS0618
        _clock = defaults.Clock;
#pragma warning restore CS0618
        _scanFrequencyTicks = defaults.ExpirationScanFrequency.Ticks;
        _logger = loggerFactory.CreateLogger<InMemorySessionStore>();
        _lastScanTicks = NowTicks();
    }

    /// <inheritdoc />
    public Task AddAsync(PermutationSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_addLock)
        {
            var now = NowTicks();
            if (_sessions.Count >= _maxSessions)
            {
                // Expired sessions still occupy room until purged: purge them synchronously before deciding.
                PurgeExpired(now);
                if (_sessions.Count >= _maxSessions)
                {
                    throw new SessionCapacityExceededException(_maxSessions);
                }
            }

            if (!_sessions.TryAdd(session.Id, new SessionEntry(session, now)))
            {
                throw new InvalidOperationException($"Session '{session.Id}' is already stored.");
            }
        }

        PurgeIfDue();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync<TResult>(
        Guid sessionId,
        Func<PermutationSession, TResult> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        PurgeIfDue();

        if (!_sessions.TryGetValue(sessionId, out var entry))
        {
            throw new SessionNotFoundException(sessionId);
        }

        var now = NowTicks();
        if (!entry.TryTouch(now, _slidingTicks))
        {
            // Expired (or concurrently purged): make sure it is gone, and never revive it.
            if (entry.TryKill(now, _slidingTicks))
            {
                _sessions.TryRemove(new KeyValuePair<Guid, SessionEntry>(sessionId, entry));
            }

            throw new SessionNotFoundException(sessionId);
        }

        await entry.Lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return operation(entry.Session);
        }
        finally
        {
            entry.Lock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _sessions.Clear();

    private long NowTicks() => (_clock?.UtcNow ?? DateTimeOffset.UtcNow).UtcTicks;

    /// <summary>Removes every expired entry. Returns the number removed.</summary>
    private int PurgeExpired(long now)
    {
        var removed = 0;
        foreach (var (id, entry) in _sessions)
        {
            if (entry.TryKill(now, _slidingTicks) &&
                _sessions.TryRemove(new KeyValuePair<Guid, SessionEntry>(id, entry)))
            {
                removed++;
            }
        }

        if (removed > 0)
        {
            _logger.LogDebug("Purged {Count} expired session(s)", removed);
        }

        return removed;
    }

    /// <summary>Runs <see cref="PurgeExpired"/> if the scan frequency has elapsed; only one caller wins per period.</summary>
    private void PurgeIfDue()
    {
        var now = NowTicks();
        var last = Interlocked.Read(ref _lastScanTicks);
        if (now - last < _scanFrequencyTicks || Interlocked.CompareExchange(ref _lastScanTicks, now, last) != last)
        {
            return;
        }

        PurgeExpired(now);
    }

    private sealed class SessionEntry(PermutationSession session, long createdTicks)
    {
        /// <summary>Marker stored in <see cref="_lastAccessTicks"/> once the entry has been declared expired.</summary>
        private const long Dead = long.MinValue;

        private long _lastAccessTicks = createdTicks;

        public PermutationSession Session { get; } = session;

        public SemaphoreSlim Lock { get; } = new(1, 1);

        /// <summary>Refreshes the last access time if the entry is still live; returns <c>false</c> if it is expired.</summary>
        public bool TryTouch(long now, long slidingTicks)
        {
            while (true)
            {
                var last = Interlocked.Read(ref _lastAccessTicks);
                if (last == Dead || now - last >= slidingTicks)
                {
                    return false;
                }

                // Never move the timestamp backwards (concurrent touches with slightly older "now" values).
                var next = Math.Max(last, now);
                if (Interlocked.CompareExchange(ref _lastAccessTicks, next, last) == last)
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// Declares the entry dead if it is expired at <paramref name="now"/>; returns <c>true</c> if it is dead
        /// afterwards (expired now or already dead), <c>false</c> if it is live.
        /// </summary>
        public bool TryKill(long now, long slidingTicks)
        {
            while (true)
            {
                var last = Interlocked.Read(ref _lastAccessTicks);
                if (last == Dead)
                {
                    return true;
                }

                if (now - last < slidingTicks)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(ref _lastAccessTicks, Dead, last) == last)
                {
                    return true;
                }
            }
        }
    }
}
