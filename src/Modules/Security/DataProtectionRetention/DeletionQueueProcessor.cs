using ALKAROS.Audit.EventStore;

namespace ALKAROS.Security.DataProtectionRetention;

/// <summary>
/// Processes the deletion queue <see cref="RetentionExecutionService"/>
/// populates for Delete-class disposals: hard-deletes each queued row.
/// Idempotent — an empty queue, or a row already purged by an earlier or
/// concurrent run, both fall through as a no-op rather than an error. Each
/// purge is recorded through V1-OPS-001's audit foundation before the row
/// disappears — the purge itself is the only lasting evidence otherwise.
/// </summary>
public sealed class DeletionQueueProcessor
{
    private readonly IRetentionSubjectStore _store;
    private readonly IAuditEventStore _auditStore;

    public DeletionQueueProcessor(IRetentionSubjectStore store, IAuditEventStore auditStore)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
    }

    public async Task<IReadOnlyList<Guid>> ProcessAsync(string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        var queued = await _store.GetDeletionQueueAsync(cancellationToken);
        foreach (var id in queued)
        {
            await _auditStore.AppendAsync(
                new AuditEvent(
                    Guid.NewGuid(),
                    "RetentionSubjectPurged",
                    "RetentionSubject",
                    id,
                    actorType: "System",
                    correlationId: correlationId),
                cancellationToken);
            await _store.PurgeAsync(id, cancellationToken);
        }
        return queued;
    }
}
