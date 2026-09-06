using Npgsql;

namespace ALKAROS.Inventory.BalanceProjection;

public interface IStockBalanceRepository
{
    Task<StockBalance?> GetByItemAndLocationAsync(Guid stockItemId, Guid stockLocationId, CancellationToken ct = default);
    Task<IReadOnlyList<StockBalance>> GetByStockItemAsync(Guid stockItemId, CancellationToken ct = default);
    Task<IReadOnlyList<StockBalance>> GetByLocationAsync(Guid stockLocationId, CancellationToken ct = default);
    Task<IReadOnlyList<StockBalance>> GetAllAsync(CancellationToken ct = default);
    Task<StockBalance> ApplyOnHandDeltaAsync(Guid stockItemId, Guid stockLocationId, decimal onHandDelta, CancellationToken ct = default);

    /// <summary>
    /// Applies the delta using the caller's own connection and transaction,
    /// so it commits atomically with the caller's other writes (V0-ARC-001
    /// row 11: Production, Purchasing → Inventory direct-call edge). The
    /// caller owns the transaction's lifetime (commit/rollback).
    /// </summary>
    Task<StockBalance> ApplyOnHandDeltaAsync(Guid stockItemId, Guid stockLocationId, decimal onHandDelta, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default);

    Task SetExactBalanceAsync(Guid stockItemId, Guid stockLocationId, decimal onHandQuantity, CancellationToken ct = default);
    Task ResetAllBalancesAsync(CancellationToken ct = default);
}
