using ALKAROS.Billing.BillFoundation;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.PaymentAggregate;

namespace ALKAROS.Billing.PaymentClosure;

/// <summary>
/// Pure, side-effect-free computation of a Bill's payment-closure
/// projection from already-loaded Payment/PaymentAllocation state
/// (V13-ALC-002) — testable without a database, same separation
/// <see cref="ALKAROS.Payments.Allocations.Persistence.PaymentAllocationFactory"/>
/// already established for allocation validation. The caller
/// (<see cref="BillPaymentClosureProjector"/>) is responsible for loading
/// real, current state.
/// </summary>
public static class BillPaymentClosureCalculator
{
    public static BillPaymentClosureProjection Compute(
        Bill bill,
        IReadOnlyList<Payment> payments,
        IReadOnlyList<PaymentAllocation> allocations)
    {
        ArgumentNullException.ThrowIfNull(bill);
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(allocations);

        // Refunded/PartiallyRefunded are out of this task's scope (V13-ALC-004's
        // own remit — netting a refund out of what still counts as "paid").
        // No code anywhere produces either status yet, so only Approved is
        // ever actually reachable here; Pending/Unknown/Declined/Cancelled
        // must never contribute (this task's own Acceptance evidence).
        var approvedPaymentIds = payments
            .Where(p => p.Status == PaymentStatus.Approved)
            .Select(p => p.Id)
            .ToHashSet();

        var paidTotal = payments
            .Where(p => approvedPaymentIds.Contains(p.Id))
            .Sum(p => p.ApprovedAmount ?? 0m);
        var changeTotal = payments
            .Where(p => approvedPaymentIds.Contains(p.Id))
            .Sum(p => p.ChangeAmount);

        // Cross-referenced against the authoritative Payment set (the
        // Goal's own wording) rather than trusting every allocation row at
        // face value — an allocation whose Payment is not (or no longer)
        // Approved contributes nothing, regardless of what
        // IPaymentAllocationRepository itself enforced at write time.
        var allocatedTotal = allocations
            .Where(a => approvedPaymentIds.Contains(a.PaymentId))
            .Sum(a => a.Amount);

        var blockers = new List<BillClosureBlocker>();
        if (allocatedTotal < bill.PayableAmount)
            blockers.Add(BillClosureBlocker.NotFullyAllocated);
        if (payments.Any(p => p.Status is PaymentStatus.Pending or PaymentStatus.ReconciliationRequired))
            blockers.Add(BillClosureBlocker.HasPendingPayment);
        if (payments.Any(p => p.Status == PaymentStatus.Unknown))
            blockers.Add(BillClosureBlocker.HasUnknownPayment);

        // AllocateAsync's own remaining-payable invariant (V0-DOM-004) means
        // allocatedTotal can never legitimately exceed PayableAmount — >=
        // rather than == only guards against that invariant ever being
        // violated upstream, it never changes the outcome in practice.
        var paymentSatisfied = bill.PayableAmount > 0 && allocatedTotal >= bill.PayableAmount;

        return new BillPaymentClosureProjection(
            bill.Id,
            bill.CurrencyCode,
            bill.PayableAmount,
            allocatedTotal,
            paidTotal,
            changeTotal,
            paymentSatisfied,
            blockers);
    }
}
