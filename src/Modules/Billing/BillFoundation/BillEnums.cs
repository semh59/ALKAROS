namespace ALKAROS.Billing.BillFoundation;

/// <summary>
/// Canonical Bill lifecycle states (PDF:I.46A, PDF:II.5.2, PDF:III.7.1).
/// Database status values match these enum names exactly.
/// </summary>
public enum BillState
{
    Open,
    PartiallyAllocated,
    Allocated,
    PartiallyPaid,
    Paid,
    Cancelled,
    Reopened,
}

/// <summary>
/// Canonical line types for bill items (PDF:III.7.2, V0-DOM-006).
/// Database line_type values match these enum names exactly.
/// </summary>
public enum BillLineType
{
    Sale,
    Discount,
    Complimentary,
    Refund,
    Waste,
    Adjustment,
}
