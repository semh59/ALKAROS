using Npgsql;

namespace ALKAROS.Inventory.PortionReservations.Lifecycle;

public sealed class PostgresPortionReservationRepository : IPortionReservationRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresPortionReservationRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task InsertAsync(PortionReservation reservation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        const string sql = @"
            INSERT INTO inventory.portion_reservations (
                id, order_id, order_item_id, stock_item_id, stock_location_id,
                quantity, unit_code, status, version, idempotency_key,
                reserved_at, transitioned_at, transition_reason,
                created_by, transitioned_by, metadata
            ) VALUES (
                $1, $2, $3, $4, $5,
                $6, $7, $8, $9, $10,
                $11, $12, $13,
                $14, $15, $16::jsonb
            );";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(reservation.Id);
        cmd.Parameters.AddWithValue(reservation.OrderId);
        cmd.Parameters.AddWithValue(reservation.OrderItemId);
        cmd.Parameters.AddWithValue(reservation.StockItemId);
        cmd.Parameters.AddWithValue(reservation.StockLocationId);
        cmd.Parameters.AddWithValue(reservation.Quantity);
        cmd.Parameters.AddWithValue(reservation.UnitCode);
        cmd.Parameters.AddWithValue(reservation.Status.ToString());
        cmd.Parameters.AddWithValue(reservation.Version);
        cmd.Parameters.AddWithValue((object?)reservation.IdempotencyKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue(reservation.ReservedAt);
        cmd.Parameters.AddWithValue((object?)reservation.TransitionedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)reservation.TransitionReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue(reservation.CreatedBy);
        cmd.Parameters.AddWithValue((object?)reservation.TransitionedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)reservation.MetadataJson ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<PortionReservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, order_id, order_item_id, stock_item_id, stock_location_id,
                   quantity, unit_code, status, version, idempotency_key,
                   reserved_at, transitioned_at, transition_reason,
                   created_by, transitioned_by, metadata::text
            FROM inventory.portion_reservations
            WHERE id = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapRow(reader);
        }
        return null;
    }

    public async Task<PortionReservation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return null;

        const string sql = @"
            SELECT id, order_id, order_item_id, stock_item_id, stock_location_id,
                   quantity, unit_code, status, version, idempotency_key,
                   reserved_at, transitioned_at, transition_reason,
                   created_by, transitioned_by, metadata::text
            FROM inventory.portion_reservations
            WHERE idempotency_key = $1;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(idempotencyKey.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapRow(reader);
        }
        return null;
    }

    public async Task<IReadOnlyList<PortionReservation>> GetByOrderItemIdAsync(Guid orderItemId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, order_id, order_item_id, stock_item_id, stock_location_id,
                   quantity, unit_code, status, version, idempotency_key,
                   reserved_at, transitioned_at, transition_reason,
                   created_by, transitioned_by, metadata::text
            FROM inventory.portion_reservations
            WHERE order_item_id = $1
            ORDER BY reserved_at ASC;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(orderItemId);

        var list = new List<PortionReservation>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapRow(reader));
        }
        return list;
    }

    public async Task<IReadOnlyList<PortionReservation>> GetActiveByStockItemAndLocationAsync(Guid stockItemId, Guid stockLocationId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT id, order_id, order_item_id, stock_item_id, stock_location_id,
                   quantity, unit_code, status, version, idempotency_key,
                   reserved_at, transitioned_at, transition_reason,
                   created_by, transitioned_by, metadata::text
            FROM inventory.portion_reservations
            WHERE stock_item_id = $1 AND stock_location_id = $2 AND status = 'Reserved'
            ORDER BY reserved_at ASC;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(stockItemId);
        cmd.Parameters.AddWithValue(stockLocationId);

        var list = new List<PortionReservation>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(MapRow(reader));
        }
        return list;
    }

    public async Task<bool> UpdateStatusOptimisticAsync(PortionReservation reservation, int expectedVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        const string sql = @"
            UPDATE inventory.portion_reservations
            SET status = $1,
                version = $2,
                transitioned_at = $3,
                transition_reason = $4,
                transitioned_by = $5
            WHERE id = $6 AND version = $7;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue(reservation.Status.ToString());
        cmd.Parameters.AddWithValue(reservation.Version);
        cmd.Parameters.AddWithValue((object?)reservation.TransitionedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)reservation.TransitionReason ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)reservation.TransitionedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue(reservation.Id);
        cmd.Parameters.AddWithValue(expectedVersion);

        var rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected > 0;
    }

    private static PortionReservation MapRow(NpgsqlDataReader reader)
    {
        return new PortionReservation(
            id: reader.GetGuid(0),
            orderId: reader.GetGuid(1),
            orderItemId: reader.GetGuid(2),
            stockItemId: reader.GetGuid(3),
            stockLocationId: reader.GetGuid(4),
            quantity: reader.GetDecimal(5),
            unitCode: reader.GetString(6),
            status: Enum.Parse<PortionReservationStatus>(reader.GetString(7), ignoreCase: true),
            version: reader.GetInt32(8),
            idempotencyKey: reader.IsDBNull(9) ? null : reader.GetString(9),
            reservedAt: reader.GetDateTime(10),
            transitionedAt: reader.IsDBNull(11) ? null : reader.GetDateTime(11),
            transitionReason: reader.IsDBNull(12) ? null : reader.GetString(12),
            createdBy: reader.GetGuid(13),
            transitionedBy: reader.IsDBNull(14) ? null : reader.GetGuid(14),
            metadataJson: reader.IsDBNull(15) ? null : reader.GetString(15));
    }
}
