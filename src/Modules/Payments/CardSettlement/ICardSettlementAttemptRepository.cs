using Npgsql;

namespace ALKAROS.Payments.CardSettlement;

/// <summary>
/// Persistence contract for payments.card_settlement_attempts. Every method
/// takes the caller's own open connection/transaction — this repository is
/// only ever used from inside <see cref="ICardSettlementOrchestrator"/>'s
/// own atomic transaction, never standalone.
/// </summary>
public interface ICardSettlementAttemptRepository
{
    /// <summary>Loads an attempt by its idempotency key, or null if none was ever persisted.</summary>
    Task<CardSettlementAttempt?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>Inserts a new attempt row inside the caller's transaction.</summary>
    Task InsertAsync(
        CardSettlementAttempt attempt,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);
}
