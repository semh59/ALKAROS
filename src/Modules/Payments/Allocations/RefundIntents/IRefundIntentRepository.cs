using ALKAROS.Payments.Allocations.Persistence;

namespace ALKAROS.Payments.Allocations.RefundIntents;

/// <summary>Persistence contract for RefundIntent rows (V13-ALC-003).</summary>
public interface IRefundIntentRepository
{
    /// <summary>
    /// Validates and persists a new refund intent against
    /// <paramref name="allocation"/>'s own remaining refund eligibility,
    /// atomically under a per-allocation lock so concurrent requests can
    /// never together exceed it. A replay of an already-persisted
    /// <paramref name="idempotencyKey"/> returns the existing row unchanged
    /// instead of validating or inserting again (PDF:II.2.6) — this task's
    /// own Acceptance evidence: a duplicate submit must return the same intent.
    /// </summary>
    Task<RefundIntent> CreateAsync(
        Guid paymentId,
        PaymentAllocation allocation,
        decimal requestedAmount,
        string idempotencyKey,
        Guid? requestedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>Loads a refund intent by its idempotency key, or null if none was ever persisted.</summary>
    Task<RefundIntent?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Loads every refund intent recorded against an allocation, oldest first.</summary>
    Task<IReadOnlyList<RefundIntent>> GetByAllocationIdAsync(Guid paymentAllocationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a state change (the one Pending→Rejected transition) on an
    /// existing intent. Fails if the current row version differs from
    /// <paramref name="expectedRowVersion"/>. Returns the incremented row version.
    /// </summary>
    Task<long> SaveAsync(RefundIntent refundIntent, long expectedRowVersion, CancellationToken cancellationToken = default);
}
