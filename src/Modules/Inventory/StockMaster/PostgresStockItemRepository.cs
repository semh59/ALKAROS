using System.Globalization;
using Npgsql;

namespace ALKAROS.Inventory.StockMaster;

public sealed class PostgresStockItemRepository : IStockItemRepository
{
    private const int MaxUnpagedRows = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresStockItemRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task AddAsync(StockItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        const string sql = @"
            INSERT INTO inventory.stock_items (
                id, code, name, item_type, tracking_unit_code, default_location_id, is_active, created_at, row_version
            ) VALUES (
                $1, $2, $3, $4, $5, $6, $7, $8, $9
            );";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(item.Id);
        cmd.Parameters.AddWithValue(item.Code);
        cmd.Parameters.AddWithValue(item.Name);
        cmd.Parameters.AddWithValue(item.ItemType.ToString());
        cmd.Parameters.AddWithValue(item.TrackingUnitCode);
        cmd.Parameters.AddWithValue((object?)item.DefaultLocationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(item.IsActive);
        cmd.Parameters.AddWithValue(item.CreatedAt);
        cmd.Parameters.AddWithValue(item.RowVersion);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<StockItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, code, name, item_type, tracking_unit_code, default_location_id, is_active, created_at, row_version
            FROM inventory.stock_items
            WHERE id = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MapRow(reader);
        }
        return null;
    }

    public async Task<StockItem?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, code, name, item_type, tracking_unit_code, default_location_id, is_active, created_at, row_version
            FROM inventory.stock_items
            WHERE code = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(code.Trim().ToUpperInvariant());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MapRow(reader);
        }
        return null;
    }

    public async Task<IReadOnlyList<StockItem>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var sql = @"
            SELECT id, code, name, item_type, tracking_unit_code, default_location_id, is_active, created_at, row_version
            FROM inventory.stock_items";

        if (activeOnly)
            sql += " WHERE is_active = true";

        sql += $" ORDER BY code ASC LIMIT {MaxUnpagedRows + 1};";

        await using var cmd = _dataSource.CreateCommand(sql);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<StockItem>();
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

    public async Task<IReadOnlyList<StockItem>> GetByTypeAsync(StockItemType itemType, bool activeOnly = false, CancellationToken ct = default)
    {
        var sql = @"
            SELECT id, code, name, item_type, tracking_unit_code, default_location_id, is_active, created_at, row_version
            FROM inventory.stock_items
            WHERE item_type = $1";

        if (activeOnly)
            sql += " AND is_active = true";

        sql += $" ORDER BY code ASC LIMIT {MaxUnpagedRows + 1};";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(itemType.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<StockItem>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapRow(reader));
        }

        if (list.Count > MaxUnpagedRows)
        {
            throw new InvalidOperationException(
                $"GetByTypeAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");
        }

        return list;
    }

    public async Task UpdateAsync(StockItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        const string sql = @"
            UPDATE inventory.stock_items
            SET name = $2,
                item_type = $3,
                tracking_unit_code = $4,
                default_location_id = $5,
                is_active = $6,
                row_version = row_version + 1
            WHERE id = $1 AND row_version = $7
            RETURNING row_version;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(item.Id);
        cmd.Parameters.AddWithValue(item.Name);
        cmd.Parameters.AddWithValue(item.ItemType.ToString());
        cmd.Parameters.AddWithValue(item.TrackingUnitCode);
        cmd.Parameters.AddWithValue((object?)item.DefaultLocationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(item.IsActive);
        cmd.Parameters.AddWithValue(item.RowVersion);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is null or DBNull)
        {
            throw new StockMasterConcurrencyException(
                $"Optimistic concurrency violation on StockItem {item.Id}.");
        }

        item.RowVersion = Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM inventory.stock_items WHERE id = $1;";
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static StockItem MapRow(NpgsqlDataReader reader)
    {
        return new StockItem(
            id: reader.GetGuid(0),
            code: reader.GetString(1),
            name: reader.GetString(2),
            itemType: Enum.Parse<StockItemType>(reader.GetString(3), ignoreCase: true),
            trackingUnitCode: reader.GetString(4),
            defaultLocationId: reader.IsDBNull(5) ? null : reader.GetGuid(5),
            isActive: reader.GetBoolean(6),
            createdAt: reader.GetFieldValue<DateTimeOffset>(7),
            rowVersion: reader.GetInt32(8));
    }
}
