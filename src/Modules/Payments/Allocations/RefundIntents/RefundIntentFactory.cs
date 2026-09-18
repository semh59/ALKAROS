using ALKAROS.Payments.Allocations.Persistence;

namespace ALKAROS.Payments.Allocations.RefundIntents;

/// <summary>
/// Pure validation/creation logic for a new <see cref="RefundIntent"/>
/// (V13-ALC-003) — takes the already-loaded PaymentAllocation plus the
/// allocation's already-Pending refund total rather than fetching either
/// itself, so this stays a plain function testable without a database,
/// the same separation <see cref="PaymentAllocationFactory"/> already
/// established.
/// </summary>
public static class RefundIntentFactory
{
    public static RefundIntent Create(
        PaymentAllocation allocation,
        Guid paymentId,
        decimal requestedAmount,
        decimal alreadyPendingForAllocation,
        string idempotencyKey,
        Guid? requestedBy = null,
        Guid? refundIntentId = null,
        DateTimeOffset? requestedAt = null)
    {
        ArgumentNullException.ThrowIfNull(allocation);

        // Same-payment invariant: a refund intent targets an allocation
        // that actually belongs to the named payment.
        if (allocation.PaymentId != paymentId)
            throw new RefundIntentCrossPaymentException(paymentId, allocation.Id, allocation.PaymentId);

        // Cumulative eligibility snapshot (this task's own In scope): the
        // allocation's own amount is the hard ceiling - no payment_reversals
        // ledger exists yet to net against (V13-ALC-004's own table), so
        // eligibility here is purely "already-Pending intents for this
        // allocation, plus this new request, must not exceed what was
        // actually allocated."
        var remaining = allocation.Amount - alreadyPendingForAllocation;
        if (requestedAmount > remaining)
            throw new RefundIntentOverLimitException(allocation.Id, requestedAmount, remaining);

        return new RefundIntent(
            refundIntentId ?? Guid.NewGuid(),
            paymentId,
            allocation.Id,
            requestedAmount,
            idempotencyKey,
            RefundIntentStatus.Pending,
            requestedBy,
            requestedAt);
    }
}
