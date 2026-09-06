using Npgsql;

namespace ALKAROS.Inventory.MovementLedger;

public sealed class PostgresStockMovementRepository : IStockMovementRepository
{
    private const int MaxUnpagedRows = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresStockMovementRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task AppendAsync(StockMovement movement, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(movement);

        await using var cmd = _dataSource.CreateCommand(AppendSql);
        BindAppendParameters(cmd, movement);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task AppendAsync(StockMovement movement, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(movement);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        await using var cmd = new NpgsqlCommand(AppendSql, connection, transaction);
        BindAppendParameters(cmd, movement);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private const string AppendSql = @"
        INSERT INTO inventory.stock_movements (
            stock_movement_id, stock_item_id, stock_location_id, movement_type, direction,
            quantity, unit_code, source_type, source_reference_id, reason, created_by, created_at
        ) VALUES (
            $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12
        );";

    private static void BindAppendParameters(NpgsqlCommand cmd, StockMovement movement)
    {
        cmd.Parameters.AddWithValue(movement.Id);
        cmd.Parameters.AddWithValue(movement.StockItemId);
        cmd.Parameters.AddWithValue(movement.StockLocationId);
        cmd.Parameters.AddWithValue(movement.MovementType.ToString());
        cmd.Parameters.AddWithValue(movement.Direction.ToString());
        cmd.Parameters.AddWithValue(movement.Quantity);
        cmd.Parameters.AddWithValue(movement.UnitCode);
        cmd.Parameters.AddWithValue(movement.SourceType);
        cmd.Parameters.AddWithValue((object?)movement.SourceReferenceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)movement.Reason ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)movement.CreatedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue(movement.CreatedAt);
    }

    public async Task<StockMovement?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT stock_movement_id, stock_item_id, stock_location_id, movement_type, direction,
                   quantity, unit_code, source_type, source_reference_id, reason, created_by, created_at
            FROM inventory.stock_movements
            WHERE stock_movement_id = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MapRow(reader);
        }
        return null;
    }

    public async Task<IReadOnlyList<StockMovement>> GetByStockItemAsync(Guid stockItemId, CancellationToken ct = default)
    {
        string sql = $@"
            SELECT stock_movement_id, stock_item_id, stock_location_id, movement_type, direction,
                   quantity, unit_code, source_type, source_reference_id, reason, created_by, created_at
            FROM inventory.stock_movements
            WHERE stock_item_id = $1
            ORDER BY created_at ASC
            LIMIT {MaxUnpagedRows + 1};";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(stockItemId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<StockMovement>();
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

    public async Task<IReadOnlyList<StockMovement>> GetByLocationAsync(Guid stockLocationId, CancellationToken ct = default)
    {
        string sql = $@"
            SELECT stock_movement_id, stock_item_id, stock_location_id, movement_type, direction,
                   quantity, unit_code, source_type, source_reference_id, reason, created_by, created_at
            FROM inventory.stock_movements
            WHERE stock_location_id = $1
            ORDER BY created_at ASC
            LIMIT {MaxUnpagedRows + 1};";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(stockLocationId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<StockMovement>();
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

    public async Task<IReadOnlyList<StockMovement>> GetBySourceAsync(string sourceType, Guid sourceReferenceId, CancellationToken ct = default)
    {
        string sql = $@"
            SELECT stock_movement_id, stock_item_id, stock_location_id, movement_type, direction,
                   quantity, unit_code, source_type, source_reference_id, reason, created_by, created_at
            FROM inventory.stock_movements
            WHERE source_type = $1 AND source_reference_id = $2
            ORDER BY created_at ASC
            LIMIT {MaxUnpagedRows + 1};";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(sourceType.Trim());
        cmd.Parameters.AddWithValue(sourceReferenceId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<StockMovement>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(MapRow(reader));
        }

        if (list.Count > MaxUnpagedRows)
        {
            throw new InvalidOperationException(
                $"GetBySourceAsync returned more than {MaxUnpagedRows} rows; narrow the filter or paginate.");
        }

        return list;
    }

    public async Task<bool> HasReversalAsync(Guid stockMovementId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT EXISTS(
                SELECT 1 FROM inventory.stock_movements
                WHERE movement_type = 'Reversal' AND source_reference_id = $1
            );";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(stockMovementId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is true;
    }

    private static StockMovement MapRow(NpgsqlDataReader reader)
    {
        return new StockMovement(
            id: reader.GetGuid(0),
            stockItemId: reader.GetGuid(1),
            stockLocationId: reader.GetGuid(2),
            movementType: Enum.Parse<StockMovementType>(reader.GetString(3), ignoreCase: true),
            direction: Enum.Parse<MovementDirection>(reader.GetString(4), ignoreCase: true),
            quantity: reader.GetDecimal(5),
            unitCode: reader.GetString(6),
            sourceType: reader.GetString(7),
            sourceReferenceId: reader.IsDBNull(8) ? null : reader.GetGuid(8),
            reason: reader.IsDBNull(9) ? null : reader.GetString(9),
            createdBy: reader.IsDBNull(10) ? null : reader.GetGuid(10),
            createdAt: reader.GetFieldValue<DateTimeOffset>(11));
    }
}
