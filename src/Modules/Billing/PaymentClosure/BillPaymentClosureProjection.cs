namespace ALKAROS.Billing.PaymentClosure;

/// <summary>
/// A reason a Bill cannot yet reach its final closed status (V13-ALC-002).
/// This task never decides closure itself (out of scope — V13-FSC-002's
/// own job); it only reports why not, from the Bill's own authoritative
/// Payment/PaymentAllocation records.
/// </summary>
public enum BillClosureBlocker
{
    /// <summary>The allocated total has not yet reached the Bill's payable amount.</summary>
    NotFullyAllocated,

    /// <summary>At least one Payment on the Bill is still Pending or ReconciliationRequired — it may still resolve.</summary>
    HasPendingPayment,

    /// <summary>At least one Payment on the Bill is Unknown (a provider timeout awaiting reconciliation, CORR:C29).</summary>
    HasUnknownPayment,
}

/// <summary>
/// The Bill's payment-closure state, rebuilt deterministically and purely
/// from its own authoritative Payment and PaymentAllocation records
/// (V13-ALC-002, PDF:I.26-I.29/II.2.6/II.3.4-II.3.5/II.5.3/III.8). Never
/// itself persisted as mutable state — a fresh, side-effect-free
/// recomputation every time (<see cref="IBillPaymentClosureProjector.RebuildAsync"/>),
/// so it can never drift from what the ledger actually says.
/// </summary>
public sealed record BillPaymentClosureProjection(
    Guid BillId,
    string CurrencyCode,
    decimal PayableAmount,
    decimal AllocatedTotal,
    decimal PaidTotal,
    decimal ChangeTotal,
    bool PaymentSatisfied,
    IReadOnlyList<BillClosureBlocker> Blockers);
