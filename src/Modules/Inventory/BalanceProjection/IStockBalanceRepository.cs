using Npgsql;

namespace ALKAROS.Inventory.BalanceProjection;

public interface IStockBalanceRepository
{
    Task<StockBalance?> GetByItemAndLocationAsync(Guid stockItemId, Guid stockLocationId, CancellationToken ct = default);
    Task<IReadOnlyList<StockBalance>> GetByStockItemAsync(Guid stockItemId, CancellationToken ct = default);

    /// <summary>
    /// V1-RMD-156: every balance row for any of the given stock items, in one
    /// round trip — see IProductStockMappingRepository.GetByProductIdsAsync's
    /// own note. A caller that needs one (item, location) pair per item
    /// filters the result in memory instead of issuing a query per pair,
    /// which sidesteps needing a composite-key ANY() query.
    /// </summary>
    Task<IReadOnlyList<StockBalance>> GetByStockItemsAsync(IReadOnlyCollection<Guid> stockItemIds, CancellationToken ct = default);

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

    /// <summary>
    /// V1-RMD-125: applies onHandDelta only if the resulting on-hand
    /// quantity would not go negative — atomically, in a single statement,
    /// so a caller that first read the balance for a friendly pre-check and
    /// then calls this to actually apply the movement cannot lose a race to
    /// another concurrent decrease (the read-then-write gap that let two
    /// requests each pass a stale check and together drive on-hand
    /// negative). Returns null when the guard fails (insufficient stock,
    /// or a same-call check-constraint violation) instead of throwing, so
    /// the caller can re-read the real current balance for an accurate
    /// domain exception message. Runs on the caller's own connection and
    /// transaction so it commits atomically with the caller's other writes
    /// (e.g. the ledger movement).
    /// </summary>
    Task<StockBalance?> TryApplyGuardedOnHandDeltaAsync(Guid stockItemId, Guid stockLocationId, decimal onHandDelta, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default);

    Task SetExactBalanceAsync(Guid stockItemId, Guid stockLocationId, decimal onHandQuantity, CancellationToken ct = default);
    Task ResetAllBalancesAsync(CancellationToken ct = default);
}
