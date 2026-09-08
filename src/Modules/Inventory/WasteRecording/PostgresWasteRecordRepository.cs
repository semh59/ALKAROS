using Npgsql;

namespace ALKAROS.Inventory.WasteRecording;

public sealed class PostgresWasteRecordRepository : IWasteRecordRepository
{
    private const int MaxUnpagedRows = 5000;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresWasteRecordRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task InsertAsync(WasteRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await using var cmd = _dataSource.CreateCommand(InsertSql);
        BindInsertParameters(cmd, record);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task InsertAsync(WasteRecord record, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        await using var cmd = new NpgsqlCommand(InsertSql, connection, transaction);
        BindInsertParameters(cmd, record);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private const string InsertSql = @"
        INSERT INTO inventory.waste_records (
            id, stock_movement_id, stock_item_id, stock_location_id,
            waste_source, source_reference_id, idempotency_key,
            quantity, unit_code, normalized_quantity, tracking_unit_code,
            waste_reason, recorded_by, recorded_at, metadata
        ) VALUES (
            $1, $2, $3, $4,
            $5, $6, $7,
            $8, $9, $10, $11,
            $12, $13, $14, $15::jsonb
        );";

    private static void BindInsertParameters(NpgsqlCommand cmd, WasteRecord record)
    {
        cmd.Parameters.AddWithValue(record.Id);
        cmd.Parameters.AddWithValue(record.StockMovementId);
        cmd.Parameters.AddWithValue(record.StockItemId);
        cmd.Parameters.AddWithValue(record.StockLocationId);
        cmd.Parameters.AddWithValue(record.WasteSource);
        cmd.Parameters.AddWithValue((object?)record.SourceReferenceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)record.IdempotencyKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue(record.Quantity);
        cmd.Parameters.AddWithValue(record.UnitCode);
        cmd.Parameters.AddWithValue(record.NormalizedQuantity);
        cmd.Parameters.AddWithValue(record.TrackingUnitCode);
        cmd.Parameters.AddWithValue(record.WasteReason);
        cmd.Parameters.AddWithValue(record.RecordedBy);
        cmd.Parameters.AddWithValue(record.RecordedAt);
        cmd.Parameters.AddWithValue((object?)record.MetadataJson ?? DBNull.Value);
    }

    public async Task<WasteRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, stock_movement_id, stock_item_id, stock_location_id,
                   waste_source, source_reference_id, idempotency_key,
                   quantity, unit_code, normalized_quantity, tracking_unit_code,
                   waste_reason, recorded_by, recorded_at, metadata::text
            FROM inventory.waste_records
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

    public async Task<WasteRecord?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return null;

        const string sql = @"
            SELECT id, stock_movement_id, stock_item_id, stock_location_id,
                   waste_source, source_reference_id, idempotency_key,
                   quantity, unit_code, normalized_quantity, tracking_unit_code,
                   waste_reason, recorded_by, recorded_at, metadata::text
            FROM inventory.waste_records
            WHERE idempotency_key = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(idempotencyKey.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return MapRow(reader);
        }
        return null;
    }

    public async Task<IReadOnlyList<WasteRecord>> GetBySourceAsync(string wasteSource, Guid sourceReferenceId, CancellationToken ct = default)
    {
        string sql = $@"
            SELECT id, stock_movement_id, stock_item_id, stock_location_id,
                   waste_source, source_reference_id, idempotency_key,
                   quantity, unit_code, normalized_quantity, tracking_unit_code,
                   waste_reason, recorded_by, recorded_at, metadata::text
            FROM inventory.waste_records
            WHERE waste_source = $1 AND source_reference_id = $2
            ORDER BY recorded_at ASC
            LIMIT {MaxUnpagedRows + 1};";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(wasteSource.Trim());
        cmd.Parameters.AddWithValue(sourceReferenceId);

        var list = new List<WasteRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
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

    private static WasteRecord MapRow(NpgsqlDataReader reader)
    {
        return new WasteRecord(
            id: reader.GetGuid(0),
            stockMovementId: reader.GetGuid(1),
            stockItemId: reader.GetGuid(2),
            stockLocationId: reader.GetGuid(3),
            wasteSource: reader.GetString(4),
            sourceReferenceId: reader.IsDBNull(5) ? null : reader.GetGuid(5),
            idempotencyKey: reader.IsDBNull(6) ? null : reader.GetString(6),
            quantity: reader.GetDecimal(7),
            unitCode: reader.GetString(8),
            normalizedQuantity: reader.GetDecimal(9),
            trackingUnitCode: reader.GetString(10),
            wasteReason: reader.GetString(11),
            recordedBy: reader.GetGuid(12),
            recordedAt: reader.GetDateTime(13),
            metadataJson: reader.IsDBNull(14) ? null : reader.GetString(14));
    }
}
