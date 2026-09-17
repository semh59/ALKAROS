namespace ALKAROS.Payments.PaymentAggregate;

/// <summary>
/// Canonical Payment lifecycle states (V0-DOM-001, PDF:I.46A/II.5 — copied
/// unchanged). <see cref="Refunded"/> and <see cref="PartiallyRefunded"/>
/// exist here so the enum matches the locked canonical set exactly, but no
/// transition into them is implemented by this task (V13-PAY-001's own
/// Out of scope: refunds belong to V0-DOM-003/V13-ALC-003/V13-ALC-004).
/// </summary>
public enum PaymentStatus
{
    Initiated,
    Pending,
    Approved,
    Declined,
    Cancelled,
    Unknown,
    ReconciliationRequired,
    Refunded,
    PartiallyRefunded,
}
