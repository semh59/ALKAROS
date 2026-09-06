using Npgsql;

namespace ALKAROS.Inventory.MovementLedger;

public interface IStockMovementRepository
{
    Task AppendAsync(StockMovement movement, CancellationToken ct = default);

    /// <summary>
    /// Appends the movement using the caller's own connection and transaction,
    /// so it commits atomically with the caller's other writes (V0-ARC-001
    /// row 11: Production, Purchasing → Inventory direct-call edge). The
    /// caller owns the transaction's lifetime (commit/rollback).
    /// </summary>
    Task AppendAsync(StockMovement movement, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default);

    Task<StockMovement?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StockMovement>> GetByStockItemAsync(Guid stockItemId, CancellationToken ct = default);
    Task<IReadOnlyList<StockMovement>> GetByLocationAsync(Guid stockLocationId, CancellationToken ct = default);
    Task<IReadOnlyList<StockMovement>> GetBySourceAsync(string sourceType, Guid sourceReferenceId, CancellationToken ct = default);
    Task<bool> HasReversalAsync(Guid stockMovementId, CancellationToken ct = default);
}
