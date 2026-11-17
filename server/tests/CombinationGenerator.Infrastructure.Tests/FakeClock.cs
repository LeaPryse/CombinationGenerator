using Microsoft.Extensions.Internal;

namespace CombinationGenerator.Infrastructure.Tests;

#pragma warning disable CS0618 // ISystemClock is the only clock hook of MemoryCache 8.x.

/// <summary>A clock for <see cref="Microsoft.Extensions.Caching.Memory.MemoryCache"/> that tests can move forward.</summary>
public sealed class FakeClock : ISystemClock
{
    private long _ticks = DateTimeOffset.UtcNow.UtcTicks;

    public DateTimeOffset UtcNow => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

    public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}

#pragma warning restore CS0618
