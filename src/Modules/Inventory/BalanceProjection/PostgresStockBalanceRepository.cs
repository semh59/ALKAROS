using Npgsql;

namespace ALKAROS.Inventory.BalanceProjection;

public sealed class PostgresStockBalanceRepository : IStockBalanceRepository
{
    private const int MaxUnpagedRows = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresStockBalanceRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<StockBalance?> GetByItemAndLocationAsync(
        Guid stockItemId,
        Guid stockLocationId,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                   reserved_quantity, available_quantity, updated_at, row_version
            FROM inventory.stock_balances
            WHERE stock_item_id = $1 AND stock_location_id = $2;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(stockItemId);
        cmd.Parameters.AddWithValue(stockLocationId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MapRow(reader);
        }
        return null;
    }

    public async Task<IReadOnlyList<StockBalance>> GetByStockItemAsync(
        Guid stockItemId,
        CancellationToken ct = default)
    {
        string sql = $@"
            SELECT stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                   reserved_quantity, available_quantity, updated_at, row_version
            FROM inventory.stock_balances
            WHERE stock_item_id = $1
            ORDER BY stock_location_id
            LIMIT {MaxUnpagedRows + 1};";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(stockItemId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<StockBalance>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapRow(reader));
        }

        if (list.Count > MaxUnpagedRows)
        {
            throw new InvalidOperationException(
                $"GetByStockItemAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");
        }

        return list;
    }

    public async Task<IReadOnlyList<StockBalance>> GetByStockItemsAsync(
        IReadOnlyCollection<Guid> stockItemIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stockItemIds);
        if (stockItemIds.Count == 0) return [];

        string sql = $@"
            SELECT stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                   reserved_quantity, available_quantity, updated_at, row_version
            FROM inventory.stock_balances
            WHERE stock_item_id = ANY($1)
            ORDER BY stock_item_id, stock_location_id
            LIMIT {MaxUnpagedRows + 1};";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(stockItemIds.ToArray());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<StockBalance>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapRow(reader));
        }

        if (list.Count > MaxUnpagedRows)
        {
            throw new InvalidOperationException(
                $"GetByStockItemsAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");
        }

        return list;
    }

    public async Task<IReadOnlyList<StockBalance>> GetByLocationAsync(
        Guid stockLocationId,
        CancellationToken ct = default)
    {
        string sql = $@"
            SELECT stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                   reserved_quantity, available_quantity, updated_at, row_version
            FROM inventory.stock_balances
            WHERE stock_location_id = $1
            ORDER BY stock_item_id
            LIMIT {MaxUnpagedRows + 1};";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(stockLocationId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<StockBalance>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapRow(reader));
        }

        if (list.Count > MaxUnpagedRows)
        {
            throw new InvalidOperationException(
                $"GetByLocationAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");
        }

        return list;
    }

    public async Task<IReadOnlyList<StockBalance>> GetAllAsync(CancellationToken ct = default)
    {
        string sql = $@"
            SELECT stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                   reserved_quantity, available_quantity, updated_at, row_version
            FROM inventory.stock_balances
            ORDER BY stock_item_id, stock_location_id
            LIMIT {MaxUnpagedRows + 1};";

        await using var cmd = _dataSource.CreateCommand(sql);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<StockBalance>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapRow(reader));
        }

        if (list.Count > MaxUnpagedRows)
        {
            throw new InvalidOperationException(
                $"GetAllAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");
        }

        return list;
    }

    public async Task<StockBalance> ApplyOnHandDeltaAsync(
        Guid stockItemId,
        Guid stockLocationId,
        decimal onHandDelta,
        CancellationToken ct = default)
    {
        await using var cmd = _dataSource.CreateCommand(ApplyOnHandDeltaSql);
        BindApplyOnHandDeltaParameters(cmd, stockItemId, stockLocationId, onHandDelta);
        return await ReadAppliedBalanceAsync(cmd, ct);
    }

    public async Task<StockBalance> ApplyOnHandDeltaAsync(
        Guid stockItemId,
        Guid stockLocationId,
        decimal onHandDelta,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        await AcquireStockRowLockAsync(connection, transaction, stockItemId, stockLocationId, ct);
        await using var cmd = new NpgsqlCommand(ApplyOnHandDeltaSql, connection, transaction);
        BindApplyOnHandDeltaParameters(cmd, stockItemId, stockLocationId, onHandDelta);
        return await ReadAppliedBalanceAsync(cmd, ct);
    }

    // Found while root-causing V1-WTR-027's stock-consumption deadlock: the
    // retry there treats the symptom (a real Postgres corner case where
    // concurrent `INSERT ... ON CONFLICT DO UPDATE` writers to the SAME
    // already-existing row can deadlock under heavy contention). This
    // closes the actual root cause the same way V1-WTR-024 already closed
    // two other check-then-write races in this codebase: a
    // transaction-scoped advisory lock keyed by the exact row
    // (stock_item_id, stock_location_id) serializes concurrent writers to
    // THAT row only — every other stock item/location proceeds fully
    // concurrently, and the lock releases automatically on commit or
    // rollback. With writers to the same row now serialized instead of
    // racing, the deadlock this method's callers used to hit cannot occur
    // in the first place; SubmitOrderHandler's retry (V1-WTR-027) stays as
    // defense-in-depth for any other transient serialization failure, not
    // as the only thing standing between a popular dish and a lost order.
    private static async Task AcquireStockRowLockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid stockItemId,
        Guid stockLocationId,
        CancellationToken ct)
    {
        await using var lockCommand = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", connection, transaction);
        lockCommand.Parameters.AddWithValue($"{stockItemId:N}:{stockLocationId:N}");
        await lockCommand.ExecuteNonQueryAsync(ct);
    }

    public Task AcquireOnHandLockAsync(
        Guid stockItemId,
        Guid stockLocationId,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        return AcquireStockRowLockAsync(connection, transaction, stockItemId, stockLocationId, ct);
    }

    private const string ApplyOnHandDeltaSql = @"
        INSERT INTO inventory.stock_balances (
            stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
            reserved_quantity, available_quantity, updated_at, row_version
        ) VALUES (
            $1, $2, $3, $4, 0, $4, NOW(), 1
        )
        ON CONFLICT (stock_item_id, stock_location_id) DO UPDATE
        SET on_hand_quantity = inventory.stock_balances.on_hand_quantity + EXCLUDED.on_hand_quantity,
            available_quantity = (inventory.stock_balances.on_hand_quantity + EXCLUDED.on_hand_quantity) - inventory.stock_balances.reserved_quantity,
            updated_at = NOW(),
            row_version = inventory.stock_balances.row_version + 1
        RETURNING stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                  reserved_quantity, available_quantity, updated_at, row_version;";

    private static void BindApplyOnHandDeltaParameters(NpgsqlCommand cmd, Guid stockItemId, Guid stockLocationId, decimal onHandDelta)
    {
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(stockItemId);
        cmd.Parameters.AddWithValue(stockLocationId);
        cmd.Parameters.AddWithValue(onHandDelta);
    }

    public async Task<StockBalance?> TryApplyGuardedOnHandDeltaAsync(
        Guid stockItemId,
        Guid stockLocationId,
        decimal onHandDelta,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        await AcquireStockRowLockAsync(connection, transaction, stockItemId, stockLocationId, ct);
        await using var cmd = new NpgsqlCommand(TryApplyGuardedOnHandDeltaSql, connection, transaction);
        BindApplyOnHandDeltaParameters(cmd, stockItemId, stockLocationId, onHandDelta);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? MapRow(reader) : null;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.CheckViolation)
        {
            // A first-ever call for this item/location with a negative
            // delta (no existing row for the WHERE guard to protect) hits
            // the table's own ck_stock_balances_on_hand_non_negative CHECK
            // instead — the guard's final line of defense, not just the
            // read-then-write race this method exists to close.
            return null;
        }
    }

    // Same atomic UPSERT as ApplyOnHandDeltaSql, but the UPDATE branch only
    // fires when the resulting on-hand quantity would stay non-negative —
    // if the guard fails, ON CONFLICT leaves the existing row untouched and
    // RETURNING yields no row (V1-RMD-125).
    private const string TryApplyGuardedOnHandDeltaSql = @"
        INSERT INTO inventory.stock_balances (
            stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
            reserved_quantity, available_quantity, updated_at, row_version
        ) VALUES (
            $1, $2, $3, $4, 0, $4, NOW(), 1
        )
        ON CONFLICT (stock_item_id, stock_location_id) DO UPDATE
        SET on_hand_quantity = inventory.stock_balances.on_hand_quantity + EXCLUDED.on_hand_quantity,
            available_quantity = (inventory.stock_balances.on_hand_quantity + EXCLUDED.on_hand_quantity) - inventory.stock_balances.reserved_quantity,
            updated_at = NOW(),
            row_version = inventory.stock_balances.row_version + 1
        WHERE inventory.stock_balances.on_hand_quantity + EXCLUDED.on_hand_quantity >= 0
        RETURNING stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                  reserved_quantity, available_quantity, updated_at, row_version;";

    private static async Task<StockBalance> ReadAppliedBalanceAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MapRow(reader);
        }

        throw new InvalidOperationException("Failed to apply on-hand delta to stock balance.");
    }

    public async Task SetExactBalanceAsync(
        Guid stockItemId,
        Guid stockLocationId,
        decimal onHandQuantity,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO inventory.stock_balances (
                stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                reserved_quantity, available_quantity, updated_at, row_version
            ) VALUES (
                $1, $2, $3, $4, 0, $4, NOW(), 1
            )
            ON CONFLICT (stock_item_id, stock_location_id) DO UPDATE
            SET on_hand_quantity = EXCLUDED.on_hand_quantity,
                available_quantity = EXCLUDED.on_hand_quantity - inventory.stock_balances.reserved_quantity,
                updated_at = NOW(),
                row_version = inventory.stock_balances.row_version + 1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(stockItemId);
        cmd.Parameters.AddWithValue(stockLocationId);
        cmd.Parameters.AddWithValue(onHandQuantity);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ResetAllBalancesAsync(CancellationToken ct = default)
    {
        const string sql = "DELETE FROM inventory.stock_balances;";
        await using var cmd = _dataSource.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyDictionary<(Guid StockItemId, Guid StockLocationId), decimal>> GetAllReservedQuantitiesAsync(
        CancellationToken ct = default)
    {
        var sql = $"""
            SELECT stock_item_id, stock_location_id, reserved_quantity
            FROM inventory.stock_balances
            WHERE reserved_quantity <> 0
            LIMIT {MaxUnpagedRows + 1};
            """;

        var result = new Dictionary<(Guid, Guid), decimal>();
        await using var cmd = _dataSource.CreateCommand(sql);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result[(reader.GetGuid(0), reader.GetGuid(1))] = reader.GetDecimal(2);

        if (result.Count > MaxUnpagedRows)
            throw new InvalidOperationException(
                $"GetAllReservedQuantitiesAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");

        return result;
    }

    public async Task RestoreReservedQuantityAsync(
        Guid stockItemId, Guid stockLocationId, decimal reservedQuantity, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO inventory.stock_balances (
                stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                reserved_quantity, available_quantity, updated_at, row_version
            ) VALUES (
                $1, $2, $3, 0, $4, -$4, NOW(), 1
            )
            ON CONFLICT (stock_item_id, stock_location_id) DO UPDATE
            SET reserved_quantity = EXCLUDED.reserved_quantity,
                available_quantity = inventory.stock_balances.on_hand_quantity - EXCLUDED.reserved_quantity,
                updated_at = NOW(),
                row_version = inventory.stock_balances.row_version + 1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(stockItemId);
        cmd.Parameters.AddWithValue(stockLocationId);
        cmd.Parameters.AddWithValue(reservedQuantity);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static StockBalance MapRow(NpgsqlDataReader reader)
    {
        return new StockBalance(
            id: reader.GetGuid(0),
            stockItemId: reader.GetGuid(1),
            stockLocationId: reader.GetGuid(2),
            onHandQuantity: reader.GetDecimal(3),
            reservedQuantity: reader.GetDecimal(4),
            availableQuantity: reader.GetDecimal(5),
            updatedAt: reader.GetFieldValue<DateTimeOffset>(6),
            rowVersion: reader.GetInt32(7));
    }
}
