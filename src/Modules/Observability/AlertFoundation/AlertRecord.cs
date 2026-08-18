namespace ALKAROS.Observability.AlertFoundation;

/// <summary>
/// Immutable domain record representing an operational or system alert (V1-ALT-001, PDF:III.28.2).
/// </summary>
public sealed record AlertRecord(
    Guid AlertId,
    string AlertType,
    AlertSeverity Severity,
    AlertStatus Status,
    string Title,
    string Message,
    string? DeduplicationKey,
    string? SourceReferenceType,
    Guid? SourceReferenceId,
    DateTimeOffset OpenedAt,
    DateTimeOffset? AcknowledgedAt,
    Guid? AcknowledgedBy,
    DateTimeOffset? ResolvedAt,
    Guid? ResolvedBy,
    string? ResolutionReason,
    long RowVersion)
{
    public bool IsActive => Status is AlertStatus.Open or AlertStatus.Acknowledged or AlertStatus.Escalated;
}

/// <summary>
/// Immutable domain record representing an append-only lifecycle event on an alert (V1-ALT-001, PDF:III.28.3).
/// </summary>
public sealed record AlertEventRecord(
    Guid AlertEventId,
    Guid AlertId,
    AlertEventType EventType,
    Guid? ActorId,
    string PayloadJson,
    DateTimeOffset CreatedAt);

/// <summary>
/// Result returned when raising an alert, indicating whether a new alert was created or deduplicated into an existing active alert (V1-ALT-001).
/// </summary>
public sealed record AlertRaiseResult(
    AlertRecord Alert,
    bool IsNewAlert,
    bool WasDeduplicated);
