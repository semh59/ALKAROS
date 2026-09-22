using ALKAROS.Audit.EventStore;

namespace ALKAROS.Security.DataProtectionRetention;

/// <summary>
/// The retention sweep: finds pending (not-yet-disposed) subjects and
/// disposes the ones whose <see cref="DisposalMatrix"/>-configured retention
/// period has elapsed, dispatching on <see cref="DisposalMatrix.ActionFor"/>.
/// Legal-hold subjects and Retain-class categories are never disposed here.
/// Re-running the sweep is idempotent: a subject already disposed no longer
/// appears in the pending set, so it is never reprocessed or double-counted.
/// Each disposal is recorded through V1-OPS-001's audit foundation
/// (metadata only — category and action, never the envelope or plaintext).
/// </summary>
public sealed class RetentionExecutionService
{
    private readonly IRetentionSubjectStore _store;
    private readonly IAuditEventStore _auditStore;

    public RetentionExecutionService(IRetentionSubjectStore store, IAuditEventStore auditStore)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
    }

    public async Task<RetentionSweepResult> RunSweepAsync(
        DateTimeOffset now,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        var pending = await _store.GetPendingAsync(cancellationToken);

        var disposed = new List<Guid>();
        var skippedLegalHold = new List<Guid>();
        var skippedRetain = new List<Guid>();
        var skippedNotExpired = new List<Guid>();

        foreach (var subject in pending)
        {
            if (subject.LegalHold)
            {
                skippedLegalHold.Add(subject.Id);
                continue;
            }

            var period = DisposalMatrix.RetentionPeriodFor(subject.Category);
            if (period is null || now - subject.CreatedAt < period.Value)
            {
                skippedNotExpired.Add(subject.Id);
                continue;
            }

            var action = DisposalMatrix.ActionFor(subject.Category);
            if (action == DisposalAction.Retain)
            {
                skippedRetain.Add(subject.Id);
                continue;
            }

            await _store.MarkDisposedAsync(subject.Id, action, subject.RowVersion, cancellationToken);
            await _auditStore.AppendAsync(
                new AuditEvent(
                    Guid.NewGuid(),
                    "RetentionSubjectDisposed",
                    "RetentionSubject",
                    subject.Id,
                    actorType: "System",
                    correlationId: correlationId,
                    reason: $"Retention period elapsed for category {subject.Category}.",
                    afterStateJson: $$"""{"category":"{{subject.Category}}","action":"{{action}}"}"""),
                cancellationToken);
            disposed.Add(subject.Id);
        }

        return new RetentionSweepResult(disposed, skippedLegalHold, skippedRetain, skippedNotExpired);
    }
}
