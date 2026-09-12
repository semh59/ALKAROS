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

    /// <summary>
    /// V1-WTR-027 follow-up (2026-09-12): takes the same transaction-scoped
    /// advisory lock <see cref="ApplyOnHandDeltaAsync(Guid, Guid, decimal, NpgsqlConnection, NpgsqlTransaction, CancellationToken)"/>
    /// and <see cref="TryApplyGuardedOnHandDeltaAsync"/> already take
    /// internally, but exposed so a caller that must touch SEVERAL
    /// (stock item, location) pairs in one transaction — like consuming
    /// every ingredient an order's items and modifiers map to — can
    /// acquire all of them up front in one fixed, globally-consistent
    /// order (typically sorted by stock item id then location id) before
    /// doing any of the actual writes.
    ///
    /// This matters because the lock alone only serializes writers to the
    /// SAME row; it does nothing to stop two DIFFERENT transactions that
    /// each need locks A and B from deadlocking against each other by
    /// acquiring them in opposite order (transaction 1 takes A then waits
    /// on B while transaction 2 already holds B and waits on A) — the
    /// classic lock-ordering deadlock, and advisory locks participate in
    /// Postgres's own deadlock detection exactly like row locks do. Two
    /// concurrent orders that both consume the same two stock items (e.g.
    /// a product and its "ekstra peynir" modifier) but list their items in
    /// a different relative order can hit exactly this even with the
    /// per-row lock in place. Re-acquiring the same key again afterwards
    /// (as the two methods above still do) is a harmless no-op within the
    /// same transaction — pg_advisory_xact_lock stacks re-entrantly for
    /// the holder and releases automatically on commit/rollback.
    /// </summary>
    Task AcquireOnHandLockAsync(Guid stockItemId, Guid stockLocationId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default);
}
