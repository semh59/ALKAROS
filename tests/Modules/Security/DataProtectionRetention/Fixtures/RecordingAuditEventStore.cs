using ALKAROS.Audit.EventStore;

namespace ALKAROS.Security.DataProtectionRetention.Tests.Fixtures;

/// <summary>In-memory IAuditEventStore test double — PostgresAuditEventStore's own correctness is V1-OPS-001's concern, not this task's.</summary>
public sealed class RecordingAuditEventStore : IAuditEventStore
{
    private readonly List<AuditEvent> _events = [];

    public IReadOnlyList<AuditEvent> Events => _events;

    public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        auditEvent.Validate();
        _events.Add(auditEvent);
        return Task.CompletedTask;
    }

    public Task AppendBatchAsync(IEnumerable<AuditEvent> auditEvents, CancellationToken cancellationToken = default)
    {
        foreach (var auditEvent in auditEvents)
        {
            auditEvent.Validate();
            _events.Add(auditEvent);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEvent>> GetByAggregateAsync(string aggregateType, Guid aggregateId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AuditEvent>>(
            _events.Where(e => e.AggregateType == aggregateType && e.AggregateId == aggregateId).ToList());

    public Task<IReadOnlyList<AuditEvent>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AuditEvent>>(
            _events.Where(e => e.CorrelationId == correlationId).ToList());
}
