using Npgsql;

namespace ALKAROS.Inventory.PhysicalCounts;

public sealed class PostgresPhysicalCountRepository : IPhysicalCountRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresPhysicalCountRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task AppendAsync(
        StockPhysicalCount count,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(count);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        const string sql = @"
            INSERT INTO inventory.stock_physical_counts (
                id, stock_item_id, stock_location_id, counted_quantity,
                previous_on_hand_quantity, counted_by_user_id, notes,
                resulting_movement_id, counted_at
            ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9);";

        await using var cmd = new NpgsqlCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue(count.Id);
        cmd.Parameters.AddWithValue(count.StockItemId);
        cmd.Parameters.AddWithValue(count.StockLocationId);
        cmd.Parameters.AddWithValue(count.CountedQuantity);
        cmd.Parameters.AddWithValue(count.PreviousOnHandQuantity);
        cmd.Parameters.AddWithValue(count.CountedByUserId);
        cmd.Parameters.AddWithValue((object?)count.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)count.ResultingMovementId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(count.CountedAt);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<StockPhysicalCount?> GetMostRecentBeforeAsync(
        Guid stockItemId, Guid stockLocationId, DateTimeOffset at, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, stock_item_id, stock_location_id, counted_quantity,
                   previous_on_hand_quantity, counted_by_user_id, notes,
                   resulting_movement_id, counted_at
            FROM inventory.stock_physical_counts
            WHERE stock_item_id = $1 AND stock_location_id = $2 AND counted_at <= $3
            ORDER BY counted_at DESC
            LIMIT 1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(stockItemId);
        cmd.Parameters.AddWithValue(stockLocationId);
        cmd.Parameters.AddWithValue(at);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new StockPhysicalCount(
            id: reader.GetGuid(0),
            stockItemId: reader.GetGuid(1),
            stockLocationId: reader.GetGuid(2),
            countedQuantity: reader.GetDecimal(3),
            previousOnHandQuantity: reader.GetDecimal(4),
            countedByUserId: reader.GetGuid(5),
            notes: reader.IsDBNull(6) ? null : reader.GetString(6),
            resultingMovementId: reader.IsDBNull(7) ? null : reader.GetGuid(7),
            countedAt: reader.GetFieldValue<DateTimeOffset>(8));
    }
}
