using Npgsql;

namespace ALKAROS.Inventory.WasteRecording;

public interface IWasteRecordRepository
{
    Task InsertAsync(WasteRecord record, CancellationToken ct = default);

    /// <summary>
    /// Inserts using the caller's own connection and transaction, so the
    /// waste record, the ledger movement, and the balance update commit
    /// atomically as one unit (V1-RMD-125) instead of as three independent
    /// round trips that could leave an orphaned ledger row on partial
    /// failure.
    /// </summary>
    Task InsertAsync(WasteRecord record, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default);

    Task<WasteRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WasteRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);

    /// <summary>V1-RMD-310: the idempotency lookup inside the caller's transaction (falls back for fakes).</summary>
    Task<WasteRecord?> GetByIdempotencyKeyAsync(
        string idempotencyKey, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default)
        => GetByIdempotencyKeyAsync(idempotencyKey, ct);
    Task<IReadOnlyList<WasteRecord>> GetBySourceAsync(string wasteSource, Guid sourceReferenceId, CancellationToken ct = default);
}
