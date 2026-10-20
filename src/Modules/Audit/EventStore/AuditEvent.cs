namespace ALKAROS.Audit.EventStore;

/// <summary>
/// Immutable audit log event entry.
/// Records security and operational transactions with actor, reason, correlation,
/// and sanitized before/after state snapshots.
/// </summary>
public sealed class AuditEvent
{
    public AuditEvent(
        Guid id,
        string eventName,
        string aggregateType,
        Guid aggregateId,
        string actorType,
        string correlationId,
        Guid? actorId = null,
        string? reason = null,
        string? causationId = null,
        string? beforeStateJson = null,
        string? afterStateJson = null,
        string? metadataJson = null,
        DateTimeOffset? occurredAt = null)
    {
        Id = id;
        EventName = eventName;
        AggregateType = aggregateType;
        AggregateId = aggregateId;
        ActorType = actorType;
        CorrelationId = correlationId;
        ActorId = actorId;
        Reason = reason;
        CausationId = causationId;
        BeforeStateJson = beforeStateJson;
        AfterStateJson = afterStateJson;
        MetadataJson = metadataJson;
        OccurredAt = occurredAt?.ToUniversalTime() ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }
    public string EventName { get; }
    public string AggregateType { get; }
    public Guid AggregateId { get; }
    public Guid? ActorId { get; }
    public string ActorType { get; }
    public string? Reason { get; }
    public string CorrelationId { get; }
    public string? CausationId { get; }
    public string? BeforeStateJson { get; }
    public string? AfterStateJson { get; }
    public string? MetadataJson { get; }
    public DateTimeOffset OccurredAt { get; }

    public void Validate()
    {
        if (Id == Guid.Empty)
            throw new ArgumentException("Audit event Id cannot be empty.", nameof(Id));
        if (string.IsNullOrWhiteSpace(EventName))
            throw new ArgumentException("EventName cannot be empty.", nameof(EventName));
        if (string.IsNullOrWhiteSpace(AggregateType))
            throw new ArgumentException("AggregateType cannot be empty.", nameof(AggregateType));
        if (AggregateId == Guid.Empty)
            throw new ArgumentException("AggregateId cannot be empty.", nameof(AggregateId));
        if (string.IsNullOrWhiteSpace(ActorType))
            throw new ArgumentException("ActorType cannot be empty.", nameof(ActorType));
        if (string.IsNullOrWhiteSpace(CorrelationId))
            throw new ArgumentException("CorrelationId cannot be empty.", nameof(CorrelationId));

        // Mirrors the VARCHAR limits of audit.audit_events so an oversized value
        // fails here, before any business work, instead of inside the insert.
        if (EventName.Length > 128)
            throw new ArgumentException("EventName is longer than 128 characters.", nameof(EventName));
        if (AggregateType.Length > 64)
            throw new ArgumentException("AggregateType is longer than 64 characters.", nameof(AggregateType));
        if (ActorType.Length > 32)
            throw new ArgumentException("ActorType is longer than 32 characters.", nameof(ActorType));
        if (CorrelationId.Length > 128)
            throw new ArgumentException("CorrelationId is longer than 128 characters.", nameof(CorrelationId));
        if (CausationId is { Length: > 128 })
            throw new ArgumentException("CausationId is longer than 128 characters.", nameof(CausationId));
    }
}
