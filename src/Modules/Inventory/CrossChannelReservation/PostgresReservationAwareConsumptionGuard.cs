using Npgsql;

namespace ALKAROS.Inventory.CrossChannelReservation;

/// <summary>
/// The consumption side of cross-channel arbitration. A hold converted here goes to
/// Consumed with the same effect V11-INV-007's projection applies to any terminal
/// transition (reserved_quantity down, available recomputed from on-hand) and the same
/// once-only applied-event record, so a full projection rebuild still matches.
/// </summary>
public sealed class PostgresReservationAwareConsumptionGuard : IReservationAwareConsumptionGuard
{
    public async Task RecordDirectSaleAsync(
        DirectSale sale,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sale);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (new[] { sale.OrderId, sale.OrderItemId, sale.StockItemId, sale.StockLocationId, sale.DailyMenuItemId, sale.ActorId }
                .Contains(Guid.Empty)
            || sale.StockQuantity <= 0m
            || sale.PortionCount <= 0m)
            throw new InvalidCrossChannelReservationException("A direct sale needs its ids, a stock quantity and a portion count above zero.");

        var reservationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var metadata = DailyMenuPortionEvents.BuildMetadata(
            sale.Channel, sale.ChannelOrderReference, sale.DailyMenuItemId, sale.PortionCount);

        const string sql = @"
            INSERT INTO inventory.portion_reservations (
                id, order_id, order_item_id, stock_item_id, stock_location_id, quantity, unit_code, status, version,
                idempotency_key, reserved_at, transitioned_at, transition_reason, created_by, transitioned_by, metadata
            )
            SELECT $1::uuid, $2::uuid, $3::uuid, $4::uuid, $5::uuid, $6::numeric, $7::varchar, 'Consumed', 1,
                   $8::varchar, $9::timestamptz, $9::timestamptz, 'Consumed on direct sale', $10::uuid, $10::uuid, $11::jsonb
            WHERE NOT EXISTS (SELECT 1 FROM inventory.portion_reservations WHERE order_item_id = $3::uuid)
            RETURNING id;";

        await using (var cmd = new NpgsqlCommand(sql, connection, transaction))
        {
            cmd.Parameters.AddWithValue(reservationId);
            cmd.Parameters.AddWithValue(sale.OrderId);
            cmd.Parameters.AddWithValue(sale.OrderItemId);
            cmd.Parameters.AddWithValue(sale.StockItemId);
            cmd.Parameters.AddWithValue(sale.StockLocationId);
            cmd.Parameters.AddWithValue(sale.StockQuantity);
            cmd.Parameters.AddWithValue(sale.UnitCode);
            cmd.Parameters.AddWithValue($"dsale:{sale.OrderItemId:N}");
            cmd.Parameters.AddWithValue(now);
            cmd.Parameters.AddWithValue(sale.ActorId);
            cmd.Parameters.AddWithValue(metadata);
            if (await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
                return;
        }

        await DailyMenuPortionEvents.PublishAsync(
            reservationId, "Consumed", metadata, now, connection, transaction, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ConvertOwnHoldsAndCheckAvailableAsync(
        Guid orderItemId,
        Guid stockItemId,
        Guid stockLocationId,
        decimal quantity,
        Guid actorId,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (orderItemId == Guid.Empty || stockItemId == Guid.Empty || stockLocationId == Guid.Empty)
            throw new InvalidCrossChannelReservationException("Order item, stock item and location are required.");
        if (actorId == Guid.Empty)
            throw new InvalidCrossChannelReservationException("ActorId cannot be empty.");
        if (quantity <= 0m)
            throw new InvalidCrossChannelReservationException($"Consumption quantity must be strictly positive, got {quantity}.");

        const string convertSql = @"
            UPDATE inventory.portion_reservations
            SET status = 'Consumed',
                version = version + 1,
                transitioned_at = NOW(),
                transition_reason = 'Consumed on order acceptance',
                transitioned_by = $4
            WHERE order_item_id = $1 AND stock_item_id = $2 AND stock_location_id = $3 AND status = 'Reserved'
            RETURNING id, quantity, metadata::text;";

        var converted = new List<(Guid ReservationId, decimal Quantity, string? Metadata)>();
        await using (var cmd = new NpgsqlCommand(convertSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue(orderItemId);
            cmd.Parameters.AddWithValue(stockItemId);
            cmd.Parameters.AddWithValue(stockLocationId);
            cmd.Parameters.AddWithValue(actorId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                converted.Add((reader.GetGuid(0), reader.GetDecimal(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        foreach (var (reservationId, heldQuantity, metadata) in converted)
        {
            await DailyMenuPortionEvents.PublishAsync(
                reservationId, "Consumed", metadata, DateTimeOffset.UtcNow, connection, transaction, cancellationToken)
                .ConfigureAwait(false);

            const string eventSql = @"
                INSERT INTO inventory.reservation_balance_applied_events (
                    id, reservation_id, event_type, terminal_status, stock_item_id, stock_location_id, quantity, applied_at
                ) VALUES ($1, $2, 'Terminal', 'Consumed', $3, $4, $5, NOW());";

            await using (var eventCmd = new NpgsqlCommand(eventSql, connection, transaction))
            {
                eventCmd.Parameters.AddWithValue(Guid.NewGuid());
                eventCmd.Parameters.AddWithValue(reservationId);
                eventCmd.Parameters.AddWithValue(stockItemId);
                eventCmd.Parameters.AddWithValue(stockLocationId);
                eventCmd.Parameters.AddWithValue(heldQuantity);
                await eventCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            const string releaseSql = @"
                UPDATE inventory.stock_balances
                SET reserved_quantity = GREATEST(0, reserved_quantity - $3),
                    available_quantity = on_hand_quantity - GREATEST(0, reserved_quantity - $3),
                    updated_at = NOW(),
                    row_version = row_version + 1
                WHERE stock_item_id = $1 AND stock_location_id = $2;";

            await using var releaseCmd = new NpgsqlCommand(releaseSql, connection, transaction);
            releaseCmd.Parameters.AddWithValue(stockItemId);
            releaseCmd.Parameters.AddWithValue(stockLocationId);
            releaseCmd.Parameters.AddWithValue(heldQuantity);
            await releaseCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        const string availableSql = @"
            SELECT available_quantity
            FROM inventory.stock_balances
            WHERE stock_item_id = $1 AND stock_location_id = $2;";

        await using var availableCmd = new NpgsqlCommand(availableSql, connection, transaction);
        availableCmd.Parameters.AddWithValue(stockItemId);
        availableCmd.Parameters.AddWithValue(stockLocationId);
        var available = await availableCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return available is decimal availableQuantity && availableQuantity >= quantity;
    }
}
