using CombinationGenerator.Application.Exceptions;
using CombinationGenerator.Domain.Sessions;
using CombinationGenerator.Infrastructure.Sessions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CombinationGenerator.Infrastructure.Tests.Sessions;

public sealed class InMemorySessionStoreTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(200);

    private readonly FakeClock _clock = new();
    private readonly List<InMemorySessionStore> _stores = [];

    public void Dispose()
    {
        foreach (var store in _stores)
        {
            store.Dispose();
        }
    }

    // ---- basic -----------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_AddedSession_RunsOperationOnThatInstance()
    {
        var store = CreateStore();
        var session = PermutationSession.Create(3);
        await store.AddAsync(session);

        var seen = await store.ExecuteAsync(session.Id, s => s);

        seen.Should().BeSameAs(session);
    }

    [Fact]
    public async Task ExecuteAsync_MutationIsPersisted()
    {
        var store = CreateStore();
        var session = PermutationSession.Create(3);
        await store.AddAsync(session);

        await store.ExecuteAsync(session.Id, s => s.MoveNext());
        var index = await store.ExecuteAsync(session.Id, s => s.CurrentIndex);

        index.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownSession_ThrowsSessionNotFound()
    {
        var store = CreateStore();
        var unknown = Guid.NewGuid();
        var ran = false;

        var act = () => store.ExecuteAsync(unknown, _ => ran = true);

        (await act.Should().ThrowAsync<SessionNotFoundException>()).Which.SessionId.Should().Be(unknown);
        ran.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_OperationThrows_ExceptionPropagatesAndLockIsReleased()
    {
        var store = CreateStore();
        var session = PermutationSession.Create(3);
        await store.AddAsync(session);

        var failing = () => store.ExecuteAsync<int>(session.Id, _ => throw new InvalidOperationException("boom"));
        await failing.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");

        var after = store.ExecuteAsync(session.Id, _ => 42);
        (await after.WaitAsync(Timeout)).Should().Be(42, "the lock must be released after a throwing operation");
    }

    [Fact]
    public async Task ArgumentChecks()
    {
        var store = CreateStore();

        await FluentActions.Awaiting(() => store.AddAsync(null!)).Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => store.ExecuteAsync<int>(Guid.NewGuid(), null!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddAsync_CancelledToken_Throws()
    {
        var store = CreateStore();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await FluentActions.Awaiting(() => store.AddAsync(PermutationSession.Create(3), cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    // ---- per-session serialization ----------------------------------------------

    [Fact]
    public async Task ExecuteAsync_SameSession_SecondCallWaitsWhileOtherSessionProceeds()
    {
        var store = CreateStore();
        var a = PermutationSession.Create(3);
        var b = PermutationSession.Create(3);
        await store.AddAsync(a);
        await store.AddAsync(b);

        using var gate = new ManualResetEventSlim(false);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = 0;

        var first = Task.Run(() => store.ExecuteAsync(a.Id, _ =>
        {
            firstEntered.SetResult();
            gate.Wait(Timeout).Should().BeTrue("the test must release the gate");
            return 1;
        }));
        await firstEntered.Task.WaitAsync(Timeout);

        var second = Task.Run(() => store.ExecuteAsync(a.Id, _ =>
        {
            Interlocked.Exchange(ref secondEntered, 1);
            return 2;
        }));

        // Session B is not blocked by A's lock.
        var onB = await store.ExecuteAsync(b.Id, s => s.N).WaitAsync(Timeout);
        onB.Should().Be(3);

        await Task.Delay(SettleDelay);
        Volatile.Read(ref secondEntered).Should().Be(0, "the second operation on A must wait for the first");
        second.IsCompleted.Should().BeFalse();

        gate.Set();
        (await first.WaitAsync(Timeout)).Should().Be(1);
        (await second.WaitAsync(Timeout)).Should().Be(2);
        Volatile.Read(ref secondEntered).Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ManyParallelCallsOnOneSession_NoLostUpdates()
    {
        var store = CreateStore();
        var session = PermutationSession.Create(10);
        await store.AddAsync(session);
        var counter = 0;
        var concurrent = 0;
        var maxConcurrent = 0;

        await Task.WhenAll(Enumerable.Range(0, 200).Select(_ => Task.Run(() => store.ExecuteAsync(session.Id, s =>
        {
            var now = Interlocked.Increment(ref concurrent);
            InterlockedMax(ref maxConcurrent, now);
            var read = counter;   // deliberately non-atomic read-modify-write
            Thread.Yield();
            counter = read + 1;
            s.MoveNext();
            Interlocked.Decrement(ref concurrent);
            return 0;
        })))).WaitAsync(Timeout);

        counter.Should().Be(200);
        maxConcurrent.Should().Be(1);
        (await store.ExecuteAsync(session.Id, s => s.CurrentIndex)).Should().Be(199);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledWhileWaitingForLock_ThrowsOperationCanceledAndDoesNotRun()
    {
        var store = CreateStore();
        var a = PermutationSession.Create(3);
        await store.AddAsync(a);

        using var gate = new ManualResetEventSlim(false);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Task.Run(() => store.ExecuteAsync(a.Id, _ =>
        {
            firstEntered.SetResult();
            gate.Wait(Timeout);
            return 1;
        }));
        await firstEntered.Task.WaitAsync(Timeout);

        using var cts = new CancellationTokenSource();
        var waiterRan = false;
        var waiter = store.ExecuteAsync(a.Id, _ => waiterRan = true, cts.Token);
        await Task.Delay(SettleDelay);
        waiter.IsCompleted.Should().BeFalse();

        cts.Cancel();

        await FluentActions.Awaiting(() => waiter.WaitAsync(Timeout)).Should().ThrowAsync<OperationCanceledException>();
        waiterRan.Should().BeFalse();

        gate.Set();
        await first.WaitAsync(Timeout);

        // The cancelled waiter must not have consumed or leaked the lock.
        (await store.ExecuteAsync(a.Id, _ => 7).WaitAsync(Timeout)).Should().Be(7);
        (await store.ExecuteAsync(a.Id, _ => 8).WaitAsync(Timeout)).Should().Be(8);
    }

    // ---- capacity ------------------------------------------------------------------

    [Fact]
    public async Task AddAsync_BeyondMaxSessions_ThrowsCapacityExceeded_AndKeepsExistingSessions()
    {
        var store = CreateStore(maxSessions: 2);
        var a = PermutationSession.Create(3);
        var b = PermutationSession.Create(3);
        await store.AddAsync(a);
        await store.AddAsync(b);
        var c = PermutationSession.Create(3);

        var act = () => store.AddAsync(c);

        (await act.Should().ThrowAsync<SessionCapacityExceededException>()).Which.MaxSessions.Should().Be(2);
        (await store.ExecuteAsync(a.Id, s => s.Id)).Should().Be(a.Id);
        (await store.ExecuteAsync(b.Id, s => s.Id)).Should().Be(b.Id);
        await FluentActions.Awaiting(() => store.ExecuteAsync(c.Id, s => s.Id))
            .Should().ThrowAsync<SessionNotFoundException>("a rejected session must not be stored");
    }

    [Fact]
    public async Task AddAsync_RepeatedRejections_NeverEvictLiveSessions()
    {
        var store = CreateStore(maxSessions: 3);
        var live = Enumerable.Range(0, 3).Select(_ => PermutationSession.Create(4)).ToList();
        foreach (var s in live)
        {
            await store.AddAsync(s);
        }

        for (var i = 0; i < 50; i++)
        {
            await FluentActions.Awaiting(() => store.AddAsync(PermutationSession.Create(4)))
                .Should().ThrowAsync<SessionCapacityExceededException>();
        }

        // Give a background over-capacity compaction (if any) time to run, then verify nothing was evicted.
        await Task.Delay(SettleDelay);
        foreach (var s in live)
        {
            (await store.ExecuteAsync(s.Id, x => x.Id)).Should().Be(s.Id);
        }
    }

    [Fact]
    public async Task AddAsync_ParallelBurst_StoresExactlyMaxSessions()
    {
        var store = CreateStore(maxSessions: 5);
        var sessions = Enumerable.Range(0, 50).Select(_ => PermutationSession.Create(3)).ToList();

        var results = await Task.WhenAll(sessions.Select(s => Task.Run(async () =>
        {
            try
            {
                await store.AddAsync(s);
                return true;
            }
            catch (SessionCapacityExceededException)
            {
                return false;
            }
        })));

        results.Count(ok => ok).Should().Be(5);

        var reachable = 0;
        foreach (var s in sessions)
        {
            try
            {
                await store.ExecuteAsync(s.Id, x => x.Id);
                reachable++;
            }
            catch (SessionNotFoundException)
            {
            }
        }

        reachable.Should().Be(5, "every accepted session must be retrievable, every rejected one absent");
    }

    // ---- expiration -----------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_AfterSlidingExpiration_ThrowsSessionNotFound()
    {
        var store = CreateStore(slidingMinutes: 30);
        var s = PermutationSession.Create(3);
        await store.AddAsync(s);

        _clock.Advance(TimeSpan.FromMinutes(31));

        await FluentActions.Awaiting(() => store.ExecuteAsync(s.Id, x => x.Id))
            .Should().ThrowAsync<SessionNotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_ThrowingOperation_StillRefreshesSlidingExpiration()
    {
        var store = CreateStore(slidingMinutes: 30);
        var s = PermutationSession.Create(3);
        await store.AddAsync(s);

        _clock.Advance(TimeSpan.FromMinutes(20));
        await FluentActions.Awaiting(() => store.ExecuteAsync<int>(s.Id, _ => throw new InvalidOperationException()))
            .Should().ThrowAsync<InvalidOperationException>();
        _clock.Advance(TimeSpan.FromMinutes(20)); // 40 since add, 20 since last touch

        (await store.ExecuteAsync(s.Id, x => x.Id)).Should().Be(s.Id);
    }

    [Fact]
    public async Task AddAsync_WhenFullOfExpiredSessions_PurgesThemAndAccepts()
    {
        var store = CreateStore(maxSessions: 2, slidingMinutes: 30);
        var old1 = PermutationSession.Create(3);
        var old2 = PermutationSession.Create(3);
        await store.AddAsync(old1);
        await store.AddAsync(old2);

        _clock.Advance(TimeSpan.FromMinutes(31));

        // No lookup touched the expired entries, so only AddAsync's own purge can free the room.
        var n1 = PermutationSession.Create(3);
        var n2 = PermutationSession.Create(3);
        await store.AddAsync(n1);
        await store.AddAsync(n2);

        (await store.ExecuteAsync(n1.Id, x => x.Id)).Should().Be(n1.Id);
        (await store.ExecuteAsync(n2.Id, x => x.Id)).Should().Be(n2.Id);
        await FluentActions.Awaiting(() => store.ExecuteAsync(old1.Id, x => x.Id))
            .Should().ThrowAsync<SessionNotFoundException>();
        await FluentActions.Awaiting(() => store.AddAsync(PermutationSession.Create(3)))
            .Should().ThrowAsync<SessionCapacityExceededException>();
    }

    [Fact]
    public async Task AddAsync_PurgeRemovesOnlyExpiredSessions()
    {
        var store = CreateStore(maxSessions: 2, slidingMinutes: 30);
        var active = PermutationSession.Create(3);
        var idle = PermutationSession.Create(3);
        await store.AddAsync(active);
        await store.AddAsync(idle);

        _clock.Advance(TimeSpan.FromMinutes(20));
        await store.ExecuteAsync(active.Id, x => x.Id);
        _clock.Advance(TimeSpan.FromMinutes(15)); // idle 35, active 15

        var fresh = PermutationSession.Create(3);
        await store.AddAsync(fresh);
        await FluentActions.Awaiting(() => store.AddAsync(PermutationSession.Create(3)))
            .Should().ThrowAsync<SessionCapacityExceededException>();

        (await store.ExecuteAsync(active.Id, x => x.Id)).Should().Be(active.Id);
        (await store.ExecuteAsync(fresh.Id, x => x.Id)).Should().Be(fresh.Id);
        await FluentActions.Awaiting(() => store.ExecuteAsync(idle.Id, x => x.Id))
            .Should().ThrowAsync<SessionNotFoundException>();
    }

    [Fact]
    public async Task AddAsync_PurgeOfExpiredSession_IsReliableUnderLoad()
    {
        // Contract: "expired sessions are purged before start is rejected". The same scenario as
        // AddAsync_PurgeRemovesOnlyExpiredSessions, repeated on many independent stores in parallel so that
        // MemoryCache's own background work (expiration scan / over-capacity compaction) races AddAsync's purge.
        var workers = Environment.ProcessorCount * 4;
        const int IterationsPerWorker = 1000;
        var failures = 0;

        await Task.WhenAll(Enumerable.Range(0, workers).Select(_ => Task.Run(async () =>
        {
            for (var i = 0; i < IterationsPerWorker; i++)
            {
                var clock = new FakeClock();
                using var store = CreateUntrackedStore(clock, maxSessions: 2);
                var active = PermutationSession.Create(3);
                await store.AddAsync(active);
                await store.AddAsync(PermutationSession.Create(3));

                clock.Advance(TimeSpan.FromMinutes(20));
                await store.ExecuteAsync(active.Id, x => x.Id);
                clock.Advance(TimeSpan.FromMinutes(15)); // the second session is now expired

                try
                {
                    await store.AddAsync(PermutationSession.Create(3));
                }
                catch (SessionCapacityExceededException)
                {
                    Interlocked.Increment(ref failures);
                }
            }
        })));

        failures.Should().Be(0, "an expired session must always be purged to make room ({0} of {1} starts were rejected)",
            failures, workers * IterationsPerWorker);
    }

    // ---- helpers ----------------------------------------------------------------------

    private static InMemorySessionStore CreateUntrackedStore(FakeClock clock, int maxSessions)
    {
#pragma warning disable CS0618 // ISystemClock is the only clock hook of MemoryCache 8.x.
        return new InMemorySessionStore(
            Options.Create(new SessionOptions { MaxSessions = maxSessions, SlidingExpirationMinutes = 30 }),
            Options.Create(new MemoryCacheOptions { Clock = clock }),
            NullLoggerFactory.Instance);
#pragma warning restore CS0618
    }

    private InMemorySessionStore CreateStore(int maxSessions = 100, int slidingMinutes = 30)
    {
        var options = Options.Create(new SessionOptions
        {
            MaxSessions = maxSessions,
            SlidingExpirationMinutes = slidingMinutes,
        });
        var cacheDefaults = Options.Create(new MemoryCacheOptions
        {
#pragma warning disable CS0618 // ISystemClock is the only clock hook of MemoryCache 8.x.
            Clock = _clock,
#pragma warning restore CS0618
        });
        var store = new InMemorySessionStore(options, cacheDefaults, NullLoggerFactory.Instance);
        _stores.Add(store);
        return store;
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        do
        {
            current = Volatile.Read(ref target);
            if (value <= current)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref target, value, current) != current);
    }
}
