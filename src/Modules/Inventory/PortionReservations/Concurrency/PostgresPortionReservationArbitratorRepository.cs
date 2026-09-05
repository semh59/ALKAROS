using ALKAROS.Inventory.PortionReservations.Lifecycle;
using Npgsql;

namespace ALKAROS.Inventory.PortionReservations.Concurrency;

public sealed class PostgresPortionReservationArbitratorRepository : IPortionReservationArbitratorRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresPortionReservationArbitratorRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<ReservationArbitrationResult> TryReserveAtomicAsync(
        ArbitrateReservationCommand command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // 1. Check idempotency if idempotency key is present
            if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
            {
                const string idempSql = @"
                    SELECT id, order_id, order_item_id, stock_item_id, stock_location_id,
                           quantity, unit_code, status, version, reserved_at, created_by,
                           idempotency_key, transitioned_at, transition_reason, transitioned_by, metadata
                    FROM inventory.portion_reservations
                    WHERE idempotency_key = $1;";

                await using var idempCmd = new NpgsqlCommand(idempSql, conn, tx);
                idempCmd.Parameters.AddWithValue(command.IdempotencyKey.Trim());

                await using var idempReader = await idempCmd.ExecuteReaderAsync(ct);
                if (await idempReader.ReadAsync(ct))
                {
                    var existingRsv = MapReservation(idempReader);
                    await idempReader.CloseAsync();

                    // Read current available balance
                    const string balSql = @"
                        SELECT available_quantity
                        FROM inventory.stock_balances
                        WHERE stock_item_id = $1 AND stock_location_id = $2;";
                    await using var curBalCmd = new NpgsqlCommand(balSql, conn, tx);
                    curBalCmd.Parameters.AddWithValue(command.StockItemId);
                    curBalCmd.Parameters.AddWithValue(command.StockLocationId);
                    var curAvailObj = await curBalCmd.ExecuteScalarAsync(ct);
                    var curAvail = curAvailObj is decimal d ? d : 0m;

                    await tx.CommitAsync(ct);
                    return ReservationArbitrationResult.Replay(existingRsv, curAvail);
                }
                await idempReader.CloseAsync();
            }

            // 2. Lock stock_balances row using SELECT ... FOR UPDATE
            const string lockSql = @"
                SELECT available_quantity, on_hand_quantity, reserved_quantity
                FROM inventory.stock_balances
                WHERE stock_item_id = $1 AND stock_location_id = $2
                FOR UPDATE;";

            await using var lockCmd = new NpgsqlCommand(lockSql, conn, tx);
            lockCmd.Parameters.AddWithValue(command.StockItemId);
            lockCmd.Parameters.AddWithValue(command.StockLocationId);

            decimal availableQuantity = 0m;
            bool rowExists = false;

            await using (var reader = await lockCmd.ExecuteReaderAsync(ct))
            {
                if (await reader.ReadAsync(ct))
                {
                    rowExists = true;
                    availableQuantity = reader.GetDecimal(0);
                }
            }

            // 3. If balance row doesn't exist or insufficient stock -> OutOfStock!
            if (!rowExists || availableQuantity < command.Quantity)
            {
                await tx.RollbackAsync(ct);
                return ReservationArbitrationResult.OutOfStock(
                    availableQuantity,
                    $"Requested {command.Quantity} {command.UnitCode}, but only {availableQuantity} is available.");
            }

            // 4. Update stock_balances atomically
            const string updateBalSql = @"
                UPDATE inventory.stock_balances
                SET reserved_quantity = reserved_quantity + $3,
                    available_quantity = available_quantity - $3,
                    updated_at = NOW(),
                    row_version = row_version + 1
                WHERE stock_item_id = $1 AND stock_location_id = $2
                RETURNING available_quantity;";

            await using var updateCmd = new NpgsqlCommand(updateBalSql, conn, tx);
            updateCmd.Parameters.AddWithValue(command.StockItemId);
            updateCmd.Parameters.AddWithValue(command.StockLocationId);
            updateCmd.Parameters.AddWithValue(command.Quantity);

            var remainingAvail = (decimal)(await updateCmd.ExecuteScalarAsync(ct))!;

            // 5. Insert portion reservation
            var reservationId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;

            const string insertRsvSql = @"
                INSERT INTO inventory.portion_reservations (
                    id, order_id, order_item_id, stock_item_id, stock_location_id,
                    quantity, unit_code, status, version, idempotency_key,
                    reserved_at, created_by, metadata
                ) VALUES (
                    $1, $2, $3, $4, $5, $6, $7, 'Reserved', 1, $8, $9, $10, $11::jsonb
                );";

            await using var insertCmd = new NpgsqlCommand(insertRsvSql, conn, tx);
            insertCmd.Parameters.AddWithValue(reservationId);
            insertCmd.Parameters.AddWithValue(command.OrderId);
            insertCmd.Parameters.AddWithValue(command.OrderItemId);
            insertCmd.Parameters.AddWithValue(command.StockItemId);
            insertCmd.Parameters.AddWithValue(command.StockLocationId);
            insertCmd.Parameters.AddWithValue(command.Quantity);
            insertCmd.Parameters.AddWithValue(command.UnitCode.Trim().ToLowerInvariant());
            insertCmd.Parameters.AddWithValue((object?)command.IdempotencyKey ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue(now);
            insertCmd.Parameters.AddWithValue(command.ActorId);
            insertCmd.Parameters.AddWithValue((object?)command.MetadataJson ?? DBNull.Value);

            await insertCmd.ExecuteNonQueryAsync(ct);

            // 6. Record applied event for projection idempotency
            const string insertEventSql = @"
                INSERT INTO inventory.reservation_balance_applied_events (
                    id, reservation_id, event_type, terminal_status, stock_item_id, stock_location_id, quantity, applied_at
                ) VALUES (
                    $1, $2, 'Reserved', NULL, $3, $4, $5, $6
                );";

            await using var eventCmd = new NpgsqlCommand(insertEventSql, conn, tx);
            eventCmd.Parameters.AddWithValue(Guid.NewGuid());
            eventCmd.Parameters.AddWithValue(reservationId);
            eventCmd.Parameters.AddWithValue(command.StockItemId);
            eventCmd.Parameters.AddWithValue(command.StockLocationId);
            eventCmd.Parameters.AddWithValue(command.Quantity);
            eventCmd.Parameters.AddWithValue(now);

            await eventCmd.ExecuteNonQueryAsync(ct);

            await tx.CommitAsync(ct);

            var reservation = new PortionReservation(
                id: reservationId,
                orderId: command.OrderId,
                orderItemId: command.OrderItemId,
                stockItemId: command.StockItemId,
                stockLocationId: command.StockLocationId,
                quantity: command.Quantity,
                unitCode: command.UnitCode.Trim().ToLowerInvariant(),
                status: PortionReservationStatus.Reserved,
                version: 1,
                reservedAt: now,
                createdBy: command.ActorId,
                idempotencyKey: command.IdempotencyKey,
                metadataJson: command.MetadataJson);

            return ReservationArbitrationResult.Success(reservation, remainingAvail);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static PortionReservation MapReservation(NpgsqlDataReader reader)
    {
        return new PortionReservation(
            id: reader.GetGuid(0),
            orderId: reader.GetGuid(1),
            orderItemId: reader.GetGuid(2),
            stockItemId: reader.GetGuid(3),
            stockLocationId: reader.GetGuid(4),
            quantity: reader.GetDecimal(5),
            unitCode: reader.GetString(6),
            status: Enum.Parse<PortionReservationStatus>(reader.GetString(7)),
            version: reader.GetInt32(8),
            reservedAt: reader.GetFieldValue<DateTimeOffset>(9),
            createdBy: reader.GetGuid(10),
            idempotencyKey: reader.IsDBNull(11) ? null : reader.GetString(11),
            transitionedAt: reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
            transitionReason: reader.IsDBNull(13) ? null : reader.GetString(13),
            transitionedBy: reader.IsDBNull(14) ? null : reader.GetGuid(14),
            metadataJson: reader.IsDBNull(15) ? null : reader.GetString(15));
    }
}
