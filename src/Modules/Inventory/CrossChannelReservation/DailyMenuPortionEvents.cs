using System.Text.Json;
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
