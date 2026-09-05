using Npgsql;

namespace ALKAROS.Inventory.BalanceProjection;

public sealed class PostgresStockBalanceRepository : IStockBalanceRepository
{
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
        const string sql = @"
            SELECT stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                   reserved_quantity, available_quantity, updated_at, row_version
            FROM inventory.stock_balances
            WHERE stock_item_id = $1
            ORDER BY stock_location_id;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(stockItemId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<StockBalance>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapRow(reader));
        }
        return list;
    }

    public async Task<IReadOnlyList<StockBalance>> GetByLocationAsync(
        Guid stockLocationId,
        CancellationToken ct = default)
    {
        const string sql = @"
            SELECT stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                   reserved_quantity, available_quantity, updated_at, row_version
            FROM inventory.stock_balances
            WHERE stock_location_id = $1
            ORDER BY stock_item_id;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(stockLocationId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<StockBalance>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapRow(reader));
        }
        return list;
    }

    public async Task<IReadOnlyList<StockBalance>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = @"
            SELECT stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                   reserved_quantity, available_quantity, updated_at, row_version
            FROM inventory.stock_balances
            ORDER BY stock_item_id, stock_location_id;";

        await using var cmd = _dataSource.CreateCommand(sql);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<StockBalance>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapRow(reader));
        }
        return list;
    }

    public async Task<StockBalance> ApplyOnHandDeltaAsync(
        Guid stockItemId,
        Guid stockLocationId,
        decimal onHandDelta,
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
            SET on_hand_quantity = inventory.stock_balances.on_hand_quantity + EXCLUDED.on_hand_quantity,
                available_quantity = (inventory.stock_balances.on_hand_quantity + EXCLUDED.on_hand_quantity) - inventory.stock_balances.reserved_quantity,
                updated_at = NOW(),
                row_version = inventory.stock_balances.row_version + 1
            RETURNING stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity,
                      reserved_quantity, available_quantity, updated_at, row_version;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(stockItemId);
        cmd.Parameters.AddWithValue(stockLocationId);
        cmd.Parameters.AddWithValue(onHandDelta);

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
