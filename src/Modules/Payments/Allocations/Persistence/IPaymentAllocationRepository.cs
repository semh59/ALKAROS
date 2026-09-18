using ALKAROS.Billing.BillFoundation;
using ALKAROS.Payments.PaymentAggregate;
using Npgsql;

namespace ALKAROS.Payments.Allocations.Persistence;

/// <summary>Persistence contract for PaymentAllocation rows (V13-ALC-001).</summary>
public interface IPaymentAllocationRepository
{
    /// <summary>
    /// Validates and persists a new allocation against <paramref name="bill"/>'s
    /// remaining payable for <paramref name="payment"/>, atomically under a
    /// per-bill lock so concurrent allocation attempts can never together
    /// over-allocate the bill (V0-DOM-004). A replay of an already-persisted
    /// <paramref name="idempotencyKey"/> returns the existing row unchanged
    /// instead of validating or inserting again (PDF:II.2.6).
    /// </summary>
    Task<PaymentAllocation> AllocateAsync(
        Payment payment,
        Bill bill,
        decimal amount,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>Same as <see cref="AllocateAsync(Payment, Bill, decimal, string, CancellationToken)"/> inside the caller's transaction (V13-CSH-003 composes with this).</summary>
    Task<PaymentAllocation> AllocateAsync(
        Payment payment,
        Bill bill,
        decimal amount,
        string idempotencyKey,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>Loads an allocation by its idempotency key, or null if none was ever persisted.</summary>
    Task<PaymentAllocation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Same as <see cref="GetByIdempotencyKeyAsync(string, CancellationToken)"/> inside the caller's transaction (V13-CSH-003 composes with this).</summary>
    Task<PaymentAllocation?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>Loads every allocation recorded against a Bill, oldest first.</summary>
    Task<IReadOnlyList<PaymentAllocation>> GetByBillIdAsync(Guid billId, CancellationToken cancellationToken = default);
}
