namespace ALKAROS.Observability.AlertFoundation;

/// <summary>
/// Canonical severity levels for system and operational alerts (V1-ALT-001, PDF:III.28.2, V0-DAT-002).
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// Canonical lifecycle states of an alert (V1-ALT-001, PDF:II.5.13, PDF:III.28.2).
/// </summary>
public enum AlertStatus
{
    Open,
    Acknowledged,
    Escalated,
    Suppressed,
    Resolved
}

/// <summary>
/// Types of alert lifecycle events recorded in append-only audit trail (V1-ALT-001, PDF:III.28.3).
/// </summary>
public enum AlertEventType
{
    Created,
    Deduplicated,
    Acknowledged,
    Escalated,
    Suppressed,
    Resolved
}
