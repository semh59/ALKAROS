namespace ALKAROS.Billing.PaymentClosure;

public enum BillClosureOutcome
{
    /// <summary>Every payable amount is covered by approved payments: the bill is now Paid.</summary>
    Closed,

    /// <summary>The bill was already Paid; nothing changed (a retry or a concurrent closer got there first).</summary>
    AlreadyClosed,

    /// <summary>Not fully covered yet, or a payment is still unresolved; the bill stays open.</summary>
    NotSatisfied,

    /// <summary>The bill cannot be closed by payment (for example it is Cancelled).</summary>
    NotClosable,
}

public sealed record BillClosureResult(BillClosureOutcome Outcome, IReadOnlyList<BillClosureBlocker> Blockers);

/// <summary>
/// V1-RMD-276: turns the payment-satisfied projection (V13-ALC-002) into the actual bill
/// state. Until now nothing moved a fully paid bill to Paid, so a settled bill stayed
/// Open forever. Idempotent and safe to call after every successful tender.
/// </summary>
public interface IBillClosureService
{
    Task<BillClosureResult> TryCloseAsync(Guid billId, CancellationToken cancellationToken = default);
}
