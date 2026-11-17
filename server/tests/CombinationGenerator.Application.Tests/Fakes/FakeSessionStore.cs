using CombinationGenerator.Application.Abstractions;
using CombinationGenerator.Application.Exceptions;
using CombinationGenerator.Domain.Sessions;

namespace CombinationGenerator.Application.Tests.Fakes;

/// <summary>Dictionary-backed store; serializes operations with a single global lock (enough for tests).</summary>
internal sealed class FakeSessionStore : ISessionStore
{
    private readonly Dictionary<Guid, PermutationSession> _sessions = [];
    private readonly object _gate = new();

    public int ExecuteCount { get; private set; }

    public IReadOnlyDictionary<Guid, PermutationSession> Sessions => _sessions;

    /// <summary>Maximum number of sessions, or <c>null</c> for unbounded (mimics a bounded store).</summary>
    public int? Capacity { get; set; }

    public Task AddAsync(PermutationSession session, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (Capacity is { } capacity && _sessions.Count >= capacity)
            {
                throw new SessionCapacityExceededException(capacity);
            }

            _sessions.Add(session.Id, session);
        }

        return Task.CompletedTask;
    }

    public Task<TResult> ExecuteAsync<TResult>(
        Guid sessionId,
        Func<PermutationSession, TResult> operation,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ExecuteCount++;
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                throw new SessionNotFoundException(sessionId);
            }

            return Task.FromResult(operation(session));
        }
    }
}
