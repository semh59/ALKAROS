using System.Collections.Concurrent;

namespace ALKAROS.Security.IdentityHardening;

/// <summary>
/// Reference/test <see cref="ISuspiciousLoginAuditSink"/> — a durable,
/// queryable Postgres-backed sink is a later Host-wiring concern (this
/// task's Owned surface has no database migration); the in-process queue
/// here is real, order-preserving storage, not a stub.
/// </summary>
public sealed class InMemorySuspiciousLoginAuditSink : ISuspiciousLoginAuditSink
{
    private readonly ConcurrentQueue<SuspiciousLoginEvent> _events = new();

    public IReadOnlyList<SuspiciousLoginEvent> Events => _events.ToArray();

    public Task RecordAsync(SuspiciousLoginEvent loginEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loginEvent);
        _events.Enqueue(loginEvent);
        return Task.CompletedTask;
    }
}
