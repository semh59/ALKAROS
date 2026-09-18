namespace ALKAROS.Payments.Allocations.RefundIntents;

/// <summary>Base exception for RefundIntent domain errors (V13-ALC-003).</summary>
public abstract class RefundIntentException : Exception
{
    protected RefundIntentException(string message) : base(message) { }
}

/// <summary>Thrown when a requested refund amount is zero or negative.</summary>
public sealed class InvalidRefundIntentAmountException : RefundIntentException
{
    public InvalidRefundIntentAmountException(decimal amount)
        : base($"Requested refund amount '{amount}' must be greater than zero.")
    {
        Amount = amount;
    }

    public decimal Amount { get; }
}

/// <summary>Thrown when a requested lifecycle transition is not valid from the intent's current status.</summary>
public sealed class InvalidRefundIntentTransitionException : RefundIntentException
{
    public InvalidRefundIntentTransitionException(Guid refundIntentId, RefundIntentStatus from, RefundIntentStatus to)
        : base($"Refund intent '{refundIntentId}' cannot transition from '{from}' to '{to}'.")
    {
        RefundIntentId = refundIntentId;
        From = from;
        To = to;
    }

    public Guid RefundIntentId { get; }
    public RefundIntentStatus From { get; }
    public RefundIntentStatus To { get; }
}

/// <summary>Thrown when the targeted PaymentAllocation does not exist.</summary>
public sealed class RefundIntentAllocationNotFoundException : RefundIntentException
{
    public RefundIntentAllocationNotFoundException(Guid paymentAllocationId)
        : base($"Payment allocation '{paymentAllocationId}' was not found.")
    {
        PaymentAllocationId = paymentAllocationId;
    }

    public Guid PaymentAllocationId { get; }
}

/// <summary>
/// Thrown when a refund intent targets an allocation belonging to a
/// different Payment than the one named — same-payment invariant, mirrors
/// PaymentAllocation's own same-bill check.
/// </summary>
public sealed class RefundIntentCrossPaymentException : RefundIntentException
{
    public RefundIntentCrossPaymentException(Guid paymentId, Guid allocationId, Guid allocationPaymentId)
        : base($"Allocation '{allocationId}' belongs to payment '{allocationPaymentId}', not '{paymentId}'.")
    {
        PaymentId = paymentId;
        AllocationId = allocationId;
        AllocationPaymentId = allocationPaymentId;
    }

    public Guid PaymentId { get; }
    public Guid AllocationId { get; }
    public Guid AllocationPaymentId { get; }
}

/// <summary>
/// Thrown when the requested amount, added to the allocation's own already-
/// Pending refund intents, would exceed the allocation's own amount —
/// rejected before any provider call (this task's own Acceptance
/// evidence), since a real refund can never exceed what was actually
/// allocated.
/// </summary>
public sealed class RefundIntentOverLimitException : RefundIntentException
{
    public RefundIntentOverLimitException(Guid paymentAllocationId, decimal requestedAmount, decimal remainingEligible)
        : base($"Allocation '{paymentAllocationId}' has {remainingEligible} remaining refund eligibility; " +
               $"a request of {requestedAmount} would exceed it.")
    {
        PaymentAllocationId = paymentAllocationId;
        RequestedAmount = requestedAmount;
        RemainingEligible = remainingEligible;
    }

    public Guid PaymentAllocationId { get; }
    public decimal RequestedAmount { get; }
    public decimal RemainingEligible { get; }
}
