namespace ALKAROS.Cash.TenderHandler;

/// <summary>
/// A request to tender cash against a Bill's remaining payable
/// (V13-CSH-003, PDF:I.26-I.29/I.49/II.2.7/II.5.9/III.9). Deliberately its
/// own shape rather than <c>ALKAROS.Payments.TenderRouting.TenderRequest</c>
/// (owned by V13-PAY-002, out of this task's Owned surface) — cash needs
/// strictly more than that generic envelope carries (the amount physically
/// handed over, which cash session it posts to, and an idempotency key for
/// safe client retries); composing this into the generic router's shape is
/// V13-PAY-003's own job (this task's Handoff).
/// </summary>
public sealed record CashTenderRequest(
    Guid CashSessionId,
    Guid BillId,
    decimal AmountDue,
    decimal TenderedAmount,
    string IdempotencyKey,
    Guid? RecordedBy = null)
{
    public void Validate()
    {
        if (CashSessionId == Guid.Empty)
            throw new ArgumentException("Cash session id cannot be empty.", nameof(CashSessionId));
        if (BillId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(BillId));
        if (AmountDue <= 0)
            throw new ArgumentException("Amount due must be greater than zero.", nameof(AmountDue));
        if (TenderedAmount <= 0)
            throw new ArgumentException("Tendered amount must be greater than zero.", nameof(TenderedAmount));
        if (string.IsNullOrWhiteSpace(IdempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(IdempotencyKey));
    }
}

/// <summary>
/// The result of a successful (or idempotently replayed) cash tender: the
/// Payment it settled, the allocation it produced against the Bill, the
/// CashTransaction it posted to the drawer's ledger, and the change owed
/// back to the customer.
/// </summary>
public sealed record CashTenderResult(
    Guid PaymentId,
    Guid PaymentAllocationId,
    Guid CashTransactionId,
    decimal ApprovedAmount,
    decimal ChangeAmount,
    bool WasReplayed);
