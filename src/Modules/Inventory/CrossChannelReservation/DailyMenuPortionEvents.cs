using System.Text.Json;
using System.Text.Json.Nodes;
using ALKAROS.IntegrationContracts;
using ALKAROS.Messaging;
using Npgsql;

namespace ALKAROS.Inventory.CrossChannelReservation;

/// <summary>
/// A reservation counts as portions of a daily menu item only when its metadata carries both keys. Exactly one
/// reservation per order item carries them, so a product mapped to several stock items is counted once.
/// </summary>
internal static class DailyMenuPortionEvents
{
    public const string DailyMenuItemKey = "daily_menu_item_id";
    public const string PortionCountKey = "portion_count";

    public const string PortionsReturnedKey = "portions_returned";
    public const string PortionsWastedKey = "portions_wasted";

    /// <summary>
    /// Raises the portions given back for the order item's counted consumption to <paramref name="returnedUnits"/> of
    /// <paramref name="totalUnits"/> of its ordered portions (cumulative, so repeating a call or sending a smaller share
    /// changes nothing), and announces a change in the caller's transaction.
    /// </summary>
    public static Task ReturnShareAsync(
        Guid orderItemId,
        decimal returnedUnits,
        decimal totalUnits,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (totalUnits <= 0m || returnedUnits < 0m || returnedUnits > totalUnits)
            throw new InvalidCrossChannelReservationException(
                $"Returned units {returnedUnits} must lie between zero and the total {totalUnits}, which must be above zero.");

        return AdjustAsync(
            orderItemId,
            (count, returned, wasted) => (
                Math.Max(returned, Math.Min(count - wasted, Math.Round(count * returnedUnits / totalUnits, 3, MidpointRounding.AwayFromZero))),
                wasted),
            connection,
            transaction,
            cancellationToken);
    }

    /// <summary>Turns every counted portion of the order item that was neither given back nor wasted yet into waste.</summary>
    public static Task WasteRemainingAsync(
        Guid orderItemId,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken) =>
        AdjustAsync(orderItemId, (count, returned, _) => (returned, count - returned), connection, transaction, cancellationToken);

    private static async Task AdjustAsync(
        Guid orderItemId,
        Func<decimal, decimal, decimal, (decimal Returned, decimal Wasted)> next,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        // Only the keyed row counts; the sibling rows of a product mapped to several stock items are left alone.
        const string selectSql = @"
            SELECT id, metadata::text
            FROM inventory.portion_reservations
            WHERE order_item_id = $1 AND status = 'Consumed'
              AND metadata->>'daily_menu_item_id' IS NOT NULL AND metadata->>'portion_count' IS NOT NULL
            FOR UPDATE;";

        var rows = new List<(Guid Id, JsonObject Metadata)>();
        await using (var select = new NpgsqlCommand(selectSql, connection, transaction))
        {
            select.Parameters.AddWithValue(orderItemId);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                rows.Add((reader.GetGuid(0), JsonNode.Parse(reader.GetString(1))!.AsObject()));
        }

        foreach (var (id, metadata) in rows)
        {
            var count = metadata[PortionCountKey]!.GetValue<decimal>();
            var returned = metadata[PortionsReturnedKey]?.GetValue<decimal>() ?? 0m;
            var wasted = metadata[PortionsWastedKey]?.GetValue<decimal>() ?? 0m;
            var (newReturned, newWasted) = next(count, returned, wasted);
            if (newReturned == returned && newWasted == wasted)
                continue;

            metadata[PortionsReturnedKey] = newReturned;
            metadata[PortionsWastedKey] = newWasted;
            await using (var update = new NpgsqlCommand(
                "UPDATE inventory.portion_reservations SET metadata = $2::jsonb, version = version + 1 WHERE id = $1;",
                connection,
                transaction))
            {
                update.Parameters.AddWithValue(id);
                update.Parameters.AddWithValue(metadata.ToJsonString());
                await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await OutboxStore.EnqueueAsync(
                new OutboxEnvelope(
                    DailyMenuCounterIntegrationEventTypes.PortionAdjusted,
                    "portion_reservation",
                    id,
                    IntegrationEventSerializer.Serialize(new DailyMenuPortionAdjusted(
                        id,
                        Guid.Parse(metadata[DailyMenuItemKey]!.GetValue<string>()),
                        count,
                        newReturned,
                        newWasted,
                        DateTimeOffset.UtcNow))),
                connection,
                transaction,
                cancellationToken).ConfigureAwait(false);
        }
    }

    public static string BuildMetadata(
        ReservationChannel channel, string channelOrderReference, Guid? dailyMenuItemId, decimal portionCount)
    {
        var values = new Dictionary<string, object>
        {
            ["channel"] = channel.ToString(),
            ["channelOrderReference"] = channelOrderReference
        };
        if (dailyMenuItemId is { } itemId)
        {
            values[DailyMenuItemKey] = itemId;
            values[PortionCountKey] = portionCount;
        }

        return JsonSerializer.Serialize(values);
    }

    /// <summary>Writes the counter event in the caller's transaction; does nothing for a reservation that is not counted.</summary>
    public static async Task PublishAsync(
        Guid reservationId,
        string status,
        string? metadataJson,
        DateTimeOffset occurredAt,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (metadataJson is null)
            return;

        using var document = JsonDocument.Parse(metadataJson);
        var root = document.RootElement;
        if (!root.TryGetProperty(DailyMenuItemKey, out var item) || !root.TryGetProperty(PortionCountKey, out var count))
            return;

        await OutboxStore.EnqueueAsync(
            new OutboxEnvelope(
                DailyMenuCounterIntegrationEventTypes.PortionStatusChanged,
                "portion_reservation",
                reservationId,
                IntegrationEventSerializer.Serialize(new DailyMenuPortionStatusChanged(
                    reservationId, item.GetGuid(), count.GetDecimal(), status, occurredAt))),
            connection,
            transaction,
            cancellationToken).ConfigureAwait(false);
    }
}
