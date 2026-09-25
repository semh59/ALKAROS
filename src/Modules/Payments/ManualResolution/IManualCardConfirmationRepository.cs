using Npgsql;

namespace ALKAROS.Payments.ManualResolution;

public interface IManualCardConfirmationRepository
{
    Task InsertAsync(
        ManualCardConfirmation confirmation, NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<ManualCardConfirmation?> GetByIdAsync(
        Guid id, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default);

    Task<ManualCardConfirmation?> GetPendingByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default);

    /// <summary>Records the decision; throws <see cref="InvalidOperationException"/> when the row changed meanwhile.</summary>
    Task DecideAsync(
        Guid id, ManualCardConfirmationStatus status, Guid decidedBy, DateTimeOffset decidedAt, string? note,
        long expectedRowVersion, NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>Newest first, bounded by <paramref name="limit"/> (max 500). <paramref name="status"/> null = all.</summary>
    Task<IReadOnlyList<ManualCardConfirmation>> ListAsync(
        ManualCardConfirmationStatus? status, int limit, CancellationToken cancellationToken = default);
}
