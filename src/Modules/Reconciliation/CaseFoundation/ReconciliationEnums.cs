namespace ALKAROS.Reconciliation.CaseFoundation;

/// <summary>
/// Domain category of the discrepancy reconciliation case (V1-REC-001, PDF:III.23).
/// </summary>
public enum CaseType
{
    PaymentMismatch,
    CashVariance,
    FiscalDiscrepancy,
    OnlineOrderMismatch,
    InventoryDiscrepancy
}

/// <summary>
/// Severity level of the reconciliation case (V1-REC-001, PDF:II.5.12).
/// </summary>
public enum CaseSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Lifecycle state of the reconciliation case (V1-REC-001, PDF:II.2.21).
/// </summary>
public enum CaseStatus
{
    Open,
    Investigating,
    Resolved,
    Dismissed,
    Escalated
}

/// <summary>
/// Append-only audit action types for reconciliation case history (V1-REC-001).
/// </summary>
public enum ActionType
{
    Created,
    Deduplicated,
    StatusChanged,
    NoteAdded,
    Resolved,
    Dismissed,
    Escalated
}
