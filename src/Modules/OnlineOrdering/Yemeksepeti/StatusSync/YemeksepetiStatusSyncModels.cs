using System.Text.Json;
using ALKAROS.IntegrationContracts;
using ALKAROS.Messaging;
using Npgsql;

namespace ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;

/// <summary>The statuses the restaurant may report to the provider (Partner API v2.0.2 order update).</summary>
public enum YemeksepetiOutboundStatus
{
    /// <summary>A platform courier collects the order (LOGISTICS_DELIVERY).</summary>
    ReadyForPickup,

    /// <summary>The restaurant's own courier left with the order (VENDOR_DELIVERY).</summary>
    Dispatched,

    Cancelled
}

/// <summary>The documented cancellation reasons a restaurant can give.</summary>
public enum YemeksepetiCancellationReason
{
    Closed,
    ItemUnavailable,
    TooBusy
}

public sealed record YemeksepetiOrderLineReference(string Sku, decimal Quantity);

/// <summary>
/// A request to tell the provider about a local status change. It is written to the outbox in the
/// same transaction as the local change, so it exists exactly when that change committed, and the
/// outbox delivers it with retries.
/// </summary>
public sealed record YemeksepetiStatusUpdateRequested(
    Guid RequestId,
    string ExternalOrderId,
    YemeksepetiOutboundStatus Status,
    YemeksepetiCancellationReason? Reason,
    IReadOnlyList<YemeksepetiOrderLineReference> Items,
    DateTimeOffset RequestedAt);

public static class YemeksepetiStatusSync
{
    public const string StatusUpdateRequestedEventType = "online-ordering.yemeksepeti.status-update-requested.v1";

    private const string VendorDelivery = "VENDOR_DELIVERY";
    private const string LogisticsDelivery = "LOGISTICS_DELIVERY";

    /// <summary>
    /// The handover status the provider expects for a delivery kind: a platform courier picks up
    /// (READY_FOR_PICKUP), a restaurant courier is dispatched (DISPATCHED). Null for a kind the
    /// document does not describe.
    /// </summary>
    public static YemeksepetiOutboundStatus? HandoverStatusFor(string? transportType) => transportType switch
    {
        LogisticsDelivery => YemeksepetiOutboundStatus.ReadyForPickup,
        VendorDelivery => YemeksepetiOutboundStatus.Dispatched,
        _ => null
    };

    /// <summary>
    /// The item references the provider needs on an update (<c>sku</c> and quantity), read leniently
    /// from a stored payload — used when the payload could not be normalized into an order.
    /// </summary>
    public static IReadOnlyList<YemeksepetiOrderLineReference> ReadItemReferences(string rawPayload)
    {
        ArgumentNullException.ThrowIfNull(rawPayload);
        var references = new List<YemeksepetiOrderLineReference>();
        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return references;
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !item.TryGetProperty("sku", out var sku) || sku.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(sku.GetString()))
                    continue;
                var quantity = item.TryGetProperty("pricing", out var pricing) && pricing.ValueKind == JsonValueKind.Object
                               && pricing.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number
                               && q.TryGetDecimal(out var parsed) && parsed > 0m
                    ? parsed
                    : 1m;
                references.Add(new YemeksepetiOrderLineReference(sku.GetString()!.Trim(), quantity));
            }
        }
        catch (JsonException)
        {
            return references;
        }

        return references;
    }

    public static Task EnqueueAsync(
        YemeksepetiStatusUpdateRequested request,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Status == YemeksepetiOutboundStatus.Cancelled && request.Reason is null)
            throw new ArgumentException("A cancellation needs a documented reason.", nameof(request));
        if (request.Items.Count == 0)
            throw new ArgumentException("The provider requires the order's items on every update.", nameof(request));

        return OutboxStore.EnqueueAsync(
            new OutboxEnvelope(
                StatusUpdateRequestedEventType,
                "yemeksepeti_order",
                request.RequestId,
                IntegrationEventSerializer.Serialize(request)),
            connection,
            transaction,
            cancellationToken);
    }
}
