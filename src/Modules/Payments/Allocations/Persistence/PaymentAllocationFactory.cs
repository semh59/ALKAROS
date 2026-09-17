using ALKAROS.Billing.BillFoundation;
using ALKAROS.Payments.PaymentAggregate;

namespace ALKAROS.Payments.Allocations.Persistence;

/// <summary>
/// Pure validation/creation logic for a new <see cref="PaymentAllocation"/>
/// (V0-DOM-004). Takes the already-loaded Payment and Bill plus the bill's
/// already-allocated total rather than fetching either itself, so this
/// stays a plain function testable without a database — the caller
/// (<see cref="PostgresPaymentAllocationRepository.AllocateAsync"/>) is
/// responsible for loading real, current state under a lock.
/// </summary>
public static class PaymentAllocationFactory
{
    public static PaymentAllocation Create(
        Payment payment,
        Bill bill,
        decimal amount,
        decimal alreadyAllocatedForBill,
        string idempotencyKey,
        Guid? allocationId = null,
        DateTimeOffset? allocatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(bill);

        // Same-bill invariant (V0-DOM-004): a Payment belongs to exactly one
        // Bill; an allocation can never target a different one.
        if (payment.BillId != bill.Id)
            throw new CrossBillPaymentAllocationException(payment.Id, payment.BillId, bill.Id);

        // Currency equality: allocation.currency = payment.currency = bill.currency.
        if (!string.Equals(payment.CurrencyCode, bill.CurrencyCode, StringComparison.Ordinal))
            throw new CurrencyMismatchAllocationException(payment.CurrencyCode, bill.CurrencyCode);

        // Remaining-amount invariant: amount <= bill.payable - sum(existing allocations).
        // The surplus (e.g. cash tendered above the payable) is never allocated —
        // it is Payment.ChangeAmount, computed and enforced entirely outside
        // this factory (Payment's own constructor, V0-DOM-004 positive example 1).
        var remaining = bill.PayableAmount - alreadyAllocatedForBill;
        if (amount > remaining)
            throw new OverAllocationException(bill.Id, amount, remaining);

        return new PaymentAllocation(
            allocationId ?? Guid.NewGuid(),
            payment.Id,
            bill.Id,
            amount,
            payment.CurrencyCode,
            idempotencyKey,
            allocatedAt);
    }
}
