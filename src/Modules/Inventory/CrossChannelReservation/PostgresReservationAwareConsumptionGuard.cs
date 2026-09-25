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
            RETURNING id, quantity;";

        var converted = new List<(Guid ReservationId, decimal Quantity)>();
        await using (var cmd = new NpgsqlCommand(convertSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue(orderItemId);
            cmd.Parameters.AddWithValue(stockItemId);
            cmd.Parameters.AddWithValue(stockLocationId);
            cmd.Parameters.AddWithValue(actorId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                converted.Add((reader.GetGuid(0), reader.GetDecimal(1)));
        }

        foreach (var (reservationId, heldQuantity) in converted)
        {
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
