namespace ALKAROS.Payments.Allocations.Persistence;

/// <summary>
/// A single, append-only allocation of a Payment's approved amount against
/// a Bill's payable (V0-DOM-004, PDF:I.11-I.15/I.26-I.29/II.2.6/
/// II.3.4-II.3.5/II.5.3/III.8, CORR:C4). Never negative, never updated:
/// the surplus over what a bill still owes is never allocated (it lives in
/// Payment.ChangeAmount instead), and a refund is a separate compensating
/// record (V0-DOM-003), never a mutation of this row.
/// </summary>
public sealed class PaymentAllocation
{
    public PaymentAllocation(
        Guid id,
        Guid paymentId,
        Guid billId,
        decimal amount,
        string currencyCode,
        string idempotencyKey,
        DateTimeOffset? allocatedAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Allocation id cannot be empty.", nameof(id));
        if (paymentId == Guid.Empty)
            throw new ArgumentException("Payment id cannot be empty.", nameof(paymentId));
        if (billId == Guid.Empty)
            throw new ArgumentException("Bill id cannot be empty.", nameof(billId));
        if (string.IsNullOrWhiteSpace(currencyCode))
            throw new ArgumentException("Currency code cannot be empty.", nameof(currencyCode));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(idempotencyKey));
        // CORR:C4 / V0-DOM-004: no negative or zero allocation exists anywhere —
        // overpayment surplus is Payment.ChangeAmount, never a compensating row.
        if (amount <= 0)
            throw new InvalidPaymentAllocationAmountException(amount);

        Id = id;
        PaymentId = paymentId;
        BillId = billId;
        Amount = amount;
        CurrencyCode = currencyCode;
        IdempotencyKey = idempotencyKey;
        AllocatedAt = allocatedAt ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }
    public Guid PaymentId { get; }
    public Guid BillId { get; }
    public decimal Amount { get; }
    public string CurrencyCode { get; }
    public string IdempotencyKey { get; }
    public DateTimeOffset AllocatedAt { get; }
}
