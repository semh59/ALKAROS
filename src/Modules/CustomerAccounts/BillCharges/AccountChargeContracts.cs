namespace ALKAROS.CustomerAccounts.BillCharges;

/// <summary>
/// A request to charge a Bill's remaining payable to a customer's account
/// (V14-ACC-003, PDF:I.30-I.33/II.2.15/II.3.11/III.18). Deliberately its own
/// shape rather than a generic tender envelope - same reasoning as
/// `ALKAROS.Cash.TenderHandler.CashTenderRequest`'s own doc comment: this
/// task needs the customer the charge posts to and an idempotency key for
/// safe client retries, and has no "tendered amount"/change concept at all
/// (an account charge is always tendered = approved = AmountDue exactly).
/// </summary>
public sealed record AccountChargeRequest(
    Guid CustomerId,
    Guid BillId,
    decimal AmountDue,
    string IdempotencyKey,
    Guid? RecordedBy = null)
{
    public void Validate()
    {
        if (CustomerId == Guid.Empty)
            throw new ArgumentException("Customer id cannot be empty.", nameof(CustomerId));
        if (BillId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(BillId));
        if (AmountDue <= 0)
            throw new ArgumentException("Amount due must be greater than zero.", nameof(AmountDue));
        if (string.IsNullOrWhiteSpace(IdempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(IdempotencyKey));
    }
}

/// <summary>
/// The result of a successful (or idempotently replayed) account charge:
/// the Payment it settled, the allocation it produced against the Bill, and
/// the AccountTransaction (Charge) it posted to the customer's ledger.
/// </summary>
public sealed record AccountChargeResult(
    Guid PaymentId,
    Guid PaymentAllocationId,
    Guid AccountTransactionId,
    decimal ApprovedAmount,
    bool WasReplayed);
