namespace ALKAROS.Cash.Contracts;

/// <summary>
/// Domain event published when a new cash session is opened on a terminal (V1-CSH-001).
/// </summary>
public sealed record CashSessionOpenedEvent(
    Guid CashSessionId,
    Guid CashierUserId,
    Guid TerminalId,
    decimal OpeningBalance,
    DateTimeOffset Timestamp);

/// <summary>
/// Domain event published when physical cash counting is initiated for a session (V1-CSH-001).
/// </summary>
public sealed record CashCountStartedEvent(
    Guid CashSessionId,
    Guid CashierUserId,
    DateTimeOffset Timestamp);

/// <summary>
/// Domain event published when a physical cash count is recorded (V1-CSH-001).
/// </summary>
public sealed record CashCountRecordedEvent(
    Guid CashSessionId,
    decimal CountedAmount,
    Guid CountedBy,
    string? Notes,
    DateTimeOffset Timestamp);

/// <summary>
/// Domain event published when a cash session is finalized and closed (V1-CSH-001).
/// </summary>
public sealed record CashSessionClosedEvent(
    Guid CashSessionId,
    decimal ExpectedCash,
    decimal ActualCash,
    decimal Difference,
    Guid ClosedBy,
    bool IsSupervisorOverride,
    string? OverrideReason,
    DateTimeOffset Timestamp);

/// <summary>
/// Domain event published when a closed cash session is reconciled with fiscal/GL audit (V1-CSH-001).
/// </summary>
public sealed record CashSessionReconciledEvent(
    Guid CashSessionId,
    Guid ReconciledBy,
    string? Notes,
    DateTimeOffset Timestamp);
