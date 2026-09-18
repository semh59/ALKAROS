namespace ALKAROS.Payments.Allocations.RefundIntents;

/// <summary>
/// A refund intent's own two-state lifecycle (V13-ALC-003, In scope: the
/// Pending/Rejected intent transition). Neither state is the actual refund —
/// this table never mutates PaymentAllocation or a Payment's net-paid
/// amount (this task's own Acceptance evidence); the real reversal ledger
/// and Payment state transition are V13-ALC-004's job.
/// </summary>
public enum RefundIntentStatus
{
    /// <summary>Passed eligibility; awaiting the actual refund (V13-ALC-004, out of this task's scope).</summary>
    Pending,

    /// <summary>Declined before ever reaching a provider call — see <see cref="RefundIntent.Reject"/>.</summary>
    Rejected,
}

/// <summary>
/// A single request to refund part or all of a specific PaymentAllocation
/// (V13-ALC-003, V0-DOM-003, PDF:I.26-I.29/II.2.6/II.3.4-II.3.5/II.5.3/
/// III.8). Immutable except for the one Pending→Rejected transition
/// (<see cref="Reject"/>) — there is no Approve/Complete transition here;
/// a fulfilled intent stays Pending in this table forever, its own
/// completion recorded entirely by V13-ALC-004's separate reversal ledger.
/// </summary>
public sealed class RefundIntent
{
    public RefundIntent(
        Guid id,
        Guid paymentId,
        Guid paymentAllocationId,
        decimal requestedAmount,
        string idempotencyKey,
        RefundIntentStatus status = RefundIntentStatus.Pending,
        Guid? requestedBy = null,
        DateTimeOffset? requestedAt = null,
        DateTimeOffset? rejectedAt = null,
        string? rejectionReason = null,
        long rowVersion = 1)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Refund intent id cannot be empty.", nameof(id));
        if (paymentId == Guid.Empty)
            throw new ArgumentException("Payment id cannot be empty.", nameof(paymentId));
        if (paymentAllocationId == Guid.Empty)
            throw new ArgumentException("Payment allocation id cannot be empty.", nameof(paymentAllocationId));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(idempotencyKey));
        if (requestedAmount <= 0)
            throw new InvalidRefundIntentAmountException(requestedAmount);

        // The fact (Rejected) and its detail fields (when/why) move
        // together — same style as Payment's own status/detail invariants.
        if ((status == RefundIntentStatus.Rejected) != (rejectedAt is not null))
            throw new ArgumentException(
                $"Status '{status}' and rejectedAt '{rejectedAt?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"}' are an invalid combination.");
        if ((status == RefundIntentStatus.Rejected) != !string.IsNullOrWhiteSpace(rejectionReason))
            throw new ArgumentException(
                $"Status '{status}' requires a rejection reason if and only if it is Rejected.");

        Id = id;
        PaymentId = paymentId;
        PaymentAllocationId = paymentAllocationId;
        RequestedAmount = requestedAmount;
        IdempotencyKey = idempotencyKey;
        Status = status;
        RequestedBy = requestedBy;
        RequestedAt = requestedAt ?? DateTimeOffset.UtcNow;
        RejectedAt = rejectedAt;
        RejectionReason = rejectionReason;
        RowVersion = rowVersion;
    }

    public Guid Id { get; }
    public Guid PaymentId { get; }
    public Guid PaymentAllocationId { get; }
    public decimal RequestedAmount { get; }
    public string IdempotencyKey { get; }
    public RefundIntentStatus Status { get; }
    public Guid? RequestedBy { get; }
    public DateTimeOffset RequestedAt { get; }
    public DateTimeOffset? RejectedAt { get; }
    public string? RejectionReason { get; }
    public long RowVersion { get; }

    /// <summary>
    /// Declines a still-Pending intent (e.g. a manager cancels it, or a
    /// later provider attempt fails — that attempt itself is out of this
    /// task's scope, only the resulting state transition lives here).
    /// </summary>
    public RefundIntent Reject(string reason, DateTimeOffset? at = null)
    {
        if (Status != RefundIntentStatus.Pending)
            throw new InvalidRefundIntentTransitionException(Id, Status, RefundIntentStatus.Rejected);
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason cannot be empty.", nameof(reason));

        return new RefundIntent(
            Id, PaymentId, PaymentAllocationId, RequestedAmount, IdempotencyKey,
            RefundIntentStatus.Rejected, RequestedBy, RequestedAt, at ?? DateTimeOffset.UtcNow, reason, RowVersion);
    }
}
