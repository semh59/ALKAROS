using ALKAROS.OnlineOrdering.OrderLinks;
using ALKAROS.OnlineOrdering.Providers.Contracts;
using ALKAROS.OnlineOrdering.Yemeksepeti.OrderNormalization;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusMapping;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;
using Npgsql;

namespace ALKAROS.OnlineOrdering.Yemeksepeti.Provider;

/// <summary>
/// V12-ONL-007: Yemeksepeti behind the shared online platform contract. It only translates: the status mapping,
/// payload normalization and outbound status event are the existing Yemeksepeti components (an UNVERIFIED DRAFT of
/// the Partner API v2.0.2 contract while V0-YSP-001 is Blocked).
/// </summary>
public sealed class YemeksepetiOnlineOrderProvider : IOnlineOrderProvider
{
    private readonly YemeksepetiOrderNormalizer _normalizer;

    public YemeksepetiOnlineOrderProvider(YemeksepetiOrderNormalizer normalizer)
    {
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
    }

    public string Provider => OnlineOrderProviders.Yemeksepeti;

    public string DisplayName => "Yemeksepeti";

    public string OrderNumberPrefix => "YS-";

    public StatusMappingResult MapStatus(string externalOrderId, string providerStatus, string rawPayload) =>
        YemeksepetiStatusMapper.Map(YemeksepetiInboxProcessingStore.ReadStatusSignal(externalOrderId, providerStatus, rawPayload));

    public Task<NormalizationResult> NormalizeAsync(string rawPayload, DateTimeOffset receivedAt, CancellationToken cancellationToken = default) =>
        _normalizer.NormalizeAsync(rawPayload, receivedAt, cancellationToken);

    public IReadOnlyList<OnlineOrderLineReference> ReadItemReferences(string rawPayload) =>
        YemeksepetiStatusSync.ReadItemReferences(rawPayload)
            .Select(reference => new OnlineOrderLineReference(reference.Sku, reference.Quantity))
            .ToList();

    /// <summary>The handover follows the delivery kind recorded when the order was created.</summary>
    public async Task<OnlineOutboundStatus?> HandoverStatusAsync(
        Guid orderId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        await using var command = new NpgsqlCommand(
            """
            SELECT outcome_detail->>'transportType'
            FROM online_ordering.provider_inbox
            WHERE provider = $2 AND order_id = $1 AND processing_outcome = 'OrderCreated'
            LIMIT 1;
            """, connection, transaction);
        command.Parameters.AddWithValue(orderId);
        command.Parameters.AddWithValue(Provider);
        var transportType = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        return YemeksepetiStatusSync.HandoverStatusFor(transportType) switch
        {
            YemeksepetiOutboundStatus.ReadyForPickup => OnlineOutboundStatus.ReadyForPickup,
            YemeksepetiOutboundStatus.Dispatched => OnlineOutboundStatus.Dispatched,
            _ => null
        };
    }

    public Task RequestStatusAsync(
        OnlineOrderStatusRequest request, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return YemeksepetiStatusSync.EnqueueAsync(
            new YemeksepetiStatusUpdateRequested(
                Guid.NewGuid(),
                request.ExternalOrderId,
                request.Status switch
                {
                    OnlineOutboundStatus.ReadyForPickup => YemeksepetiOutboundStatus.ReadyForPickup,
                    OnlineOutboundStatus.Dispatched => YemeksepetiOutboundStatus.Dispatched,
                    OnlineOutboundStatus.Cancelled => YemeksepetiOutboundStatus.Cancelled,
                    _ => throw new ArgumentOutOfRangeException(nameof(request), request.Status, "Unknown outbound status.")
                },
                request.Reason switch
                {
                    null => null,
                    OnlineCancellationReason.Closed => YemeksepetiCancellationReason.Closed,
                    OnlineCancellationReason.ItemUnavailable => YemeksepetiCancellationReason.ItemUnavailable,
                    OnlineCancellationReason.TooBusy => YemeksepetiCancellationReason.TooBusy,
                    _ => throw new ArgumentOutOfRangeException(nameof(request), request.Reason, "Unknown cancellation reason.")
                },
                request.Items.Select(item => new YemeksepetiOrderLineReference(item.Sku, item.Quantity)).ToList(),
                request.RequestedAt),
            connection, transaction, cancellationToken);
    }
}
