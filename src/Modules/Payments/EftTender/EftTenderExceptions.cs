namespace ALKAROS.Payments.EftTender;

/// <summary>Base exception for EFT/Havale tender domain errors (V13-PAY-005).</summary>
public abstract class EftTenderException : Exception
{
    protected EftTenderException(string message) : base(message) { }
}

/// <summary>Thrown when the Bill named by an EFT tender request does not exist.</summary>
public sealed class EftTenderBillNotFoundException : EftTenderException
{
    public EftTenderBillNotFoundException(Guid billId)
        : base($"Bill '{billId}' was not found.")
    {
        BillId = billId;
    }

    public Guid BillId { get; }
}

/// <summary>
/// Thrown when an EFT tender amount exceeds the Bill's remaining payable.
/// EFT has no change-giving concept (unlike Cash) — an over-tender is a hard
/// rejection, never a surplus to hand back.
/// </summary>
public sealed class EftOverTenderException : EftTenderException
{
    public EftOverTenderException(Guid billId, decimal amount, decimal remainingPayable)
        : base($"EFT amount '{amount}' exceeds bill '{billId}''s remaining payable '{remainingPayable}'.")
    {
        BillId = billId;
        Amount = amount;
        RemainingPayable = remainingPayable;
    }

    public Guid BillId { get; }
    public decimal Amount { get; }
    public decimal RemainingPayable { get; }
}

/// <summary>
/// V1-RMD-258: thrown when an EFT tender targets a Bill that already has a
/// Payment sitting at <c>Pending</c>/<c>Unknown</c>/<c>ReconciliationRequired</c>
/// (e.g. an unresolved BankCard settlement attempt) — a new tender must
/// never be layered on top of an already unresolved one; the existing
/// attempt must be reconciled first (V13-REC-001).
/// </summary>
public sealed class EftUnsettledPaymentExistsException : EftTenderException
{
    public EftUnsettledPaymentExistsException(Guid billId, Guid existingPaymentId, string existingStatus)
        : base($"Bill '{billId}' already has a payment '{existingPaymentId}' at status '{existingStatus}'; " +
               "it must be reconciled before a new EFT tender can be recorded.")
    {
        BillId = billId;
        ExistingPaymentId = existingPaymentId;
        ExistingStatus = existingStatus;
    }

    public Guid BillId { get; }
    public Guid ExistingPaymentId { get; }
    public string ExistingStatus { get; }
}

/// <summary>
/// V1-RMD-258: thrown when a replayed idempotency key's recorded allocation
/// belongs to a DIFFERENT Bill than the one the current request names — a
/// genuine cross-bill idempotency-key collision, never silently accepted as
/// a valid replay for the wrong bill. Defense-in-depth: mirrors
/// <c>CardSettlementBillMismatchException</c>'s same check for
/// <c>ICardSettlementAttemptRepository</c>.
/// </summary>
public sealed class EftBillMismatchException : EftTenderException
{
    public EftBillMismatchException(string idempotencyKey, Guid recordedBillId, Guid requestedBillId)
        : base($"EFT tender '{idempotencyKey}' was recorded against bill '{recordedBillId}', " +
               $"not the requested bill '{requestedBillId}'.")
    {
        IdempotencyKey = idempotencyKey;
        RecordedBillId = recordedBillId;
        RequestedBillId = requestedBillId;
    }

    public string IdempotencyKey { get; }
    public Guid RecordedBillId { get; }
    public Guid RequestedBillId { get; }
}
