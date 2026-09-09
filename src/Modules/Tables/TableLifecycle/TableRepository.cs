namespace ALKAROS.Tables.TableLifecycle;

public interface ITableRepository
{
    Task<Table?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Table>> GetByZoneAsync(Guid zoneId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Table>> GetUnzonedAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Table table, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically moves the table to <paramref name="target"/> guarded by an
    /// optimistic row version. Returns the new row version; throws
    /// <see cref="InvalidOperationException"/> when no row was updated
    /// (missing row or stale version).
    /// </summary>
    Task<long> UpdateStatusAsync(
        Guid id,
        TableState target,
        long expectedRowVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// V12-QRO-002: same-transaction overloads for the approved QR Ordering
    /// -&gt; Table Management direct-call edge (module-dependency-rules.md
    /// row 19) and the Order -&gt; Table Management edge (row 4) — a caller
    /// that must combine a table transition with its own domain write in one
    /// atomic commit (same pattern already established for
    /// Production -&gt; Inventory, see that row's own note in the same doc).
    /// <see cref="GetByIdForUpdateAsync"/> takes a row lock (<c>FOR UPDATE</c>)
    /// so a concurrent caller in another transaction blocks until this one
    /// commits or rolls back, rather than both reading a now-stale state.
    /// </summary>
    Task<Table?> GetByIdForUpdateAsync(
        Guid id,
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<long> UpdateStatusAsync(
        Guid id,
        TableState target,
        long expectedRowVersion,
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// V12-QRO-002: backfills the soft cache pointer
    /// <c>table_mgmt.tables.current_order_id</c> once the real Order exists
    /// — a QR order reserves the table at submission time (before the Order
    /// is materialized asynchronously), so this runs later, from the Order
    /// module's own consumer (row 4's Order -&gt; Table Management edge), in
    /// the same transaction that inserts the Order row. Ownership truth is
    /// always <c>orders.orders.table_id</c> (table-reservation-policy.md);
    /// this is purely the cache pointer converging to match it. A no-op
    /// (not an error) if the table no longer points at no order or already
    /// points at this exact order — a replayed at-least-once delivery must
    /// not fail.
    /// </summary>
    Task LinkCurrentOrderAsync(
        Guid tableId,
        Guid orderId,
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default);
}

public interface IZoneRepository
{
    Task<Zone?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Zone?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Zone>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Zone zone, CancellationToken cancellationToken = default);

    Task UpdateAsync(Zone zone, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}