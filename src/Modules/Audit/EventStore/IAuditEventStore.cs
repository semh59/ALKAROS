namespace ALKAROS.Audit.EventStore;

public interface IAuditEventStore
{
    Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
    Task AppendBatchAsync(IEnumerable<AuditEvent> auditEvents, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEvent>> GetByAggregateAsync(string aggregateType, Guid aggregateId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEvent>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
}
