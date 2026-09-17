using Npgsql;

namespace ALKAROS.Payments.PaymentAggregate;

/// <summary>
/// Persistence contract for the Payment aggregate. Concurrency is guarded
/// by the optimistic row version on the payment row (same pattern as
/// IBillRepository/IOrderRepository).
/// </summary>
public interface IPaymentRepository
{
    /// <summary>Loads a Payment with its status history by id. Returns null if not found.</summary>
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Loads every Payment recorded against a Bill.</summary>
    Task<IReadOnlyList<Payment>> GetByBillIdAsync(Guid billId, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new Payment inside its own transaction.</summary>
    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);

    /// <summary>Inserts a new Payment inside the caller's transaction (V13-PAY-003/CSH-003 compose with this).</summary>
    Task AddAsync(
        Payment payment,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a state/amount change on an existing Payment inside its own
    /// transaction. Fails if the current row version differs from
    /// <paramref name="expectedRowVersion"/>. Returns the incremented row version.
    /// </summary>
    Task<long> SaveAsync(Payment payment, long expectedRowVersion, CancellationToken cancellationToken = default);

    /// <summary>Same as <see cref="SaveAsync(Payment, long, CancellationToken)"/> inside the caller's transaction.</summary>
    Task<long> SaveAsync(
        Payment payment,
        long expectedRowVersion,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);
}
