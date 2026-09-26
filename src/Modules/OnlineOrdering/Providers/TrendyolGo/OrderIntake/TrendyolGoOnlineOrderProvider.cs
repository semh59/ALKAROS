using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.OnlineOrdering.Providers.Contracts;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.StatusSync;
using Npgsql;

namespace ALKAROS.OnlineOrdering.Providers.TrendyolGo.OrderIntake;

/// <summary>
/// V12-TGO-002: Uber Eats Trendyol Go behind the shared online platform contract — its event vocabulary and package
/// reading (V12-TGO-002) and its outbound calls (V12-TGO-003): acceptance with the preparation time, readiness,
/// own-courier dispatch and restaurant cancellation, each queued in the caller's transaction. UNVERIFIED DRAFT
/// (EXT:TGO-MEAL-API; V12-TGO-001 Blocked, C106 waiver).
/// </summary>
public sealed class TrendyolGoOnlineOrderProvider : IOnlineOrderProvider
{
    /// <summary>The version of the event vocabulary below; unknown-status evidence is keyed on it.</summary>
    public const string VocabularyVersion = "tgo-meal-webhook-2026-09-27";

    private readonly TrendyolGoOrderNormalizer _normalizer;

    public TrendyolGoOnlineOrderProvider(TrendyolGoOrderNormalizer normalizer)
    {
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
    }

    public string Provider => TrendyolGoEvents.Provider;

    public string DisplayName => "Trendyol Go";

    public string OrderNumberPrefix => "TG-";

    /// <summary>
    /// <c>created</c> asks for the order; <c>cancelled</c> (a non-seller reason, such as the customer) and
    /// <c>unsupplied</c> (a seller reason, 621–627) cancel it; <c>shipped</c> and <c>delivered</c> change nothing
    /// locally (the restaurant's part ends at handover); <c>picking</c> and <c>invoiced</c> echo this restaurant's own
    /// calls. The other documented events (<c>pickupEtaCalculated</c>, <c>courierNearby</c>, <c>storeChanged</c>,
    /// <c>sellerChanged</c>) are not expected — the integrator subscribes only the five above, as the document's own
    /// sample does — so they are unknown and reach a person.
    /// </summary>
    public StatusMappingResult MapStatus(string externalOrderId, string providerStatus, string rawPayload)
    {
        ArgumentNullException.ThrowIfNull(providerStatus);
        return providerStatus switch
        {
            TrendyolGoEvents.Created => new(StatusMappingKind.Command, InternalOrderCommand.AcceptIncomingOrder, null, null, null),
            TrendyolGoEvents.Cancelled => Cancel(CancellationParty.Unrecognized, "non-seller", rawPayload),
            TrendyolGoEvents.Unsupplied => Cancel(CancellationParty.Vendor, "seller", rawPayload),
            TrendyolGoEvents.Shipped => new(StatusMappingKind.NoOp, null, null, StatusNoOpReason.HandedToCourier, null),
            TrendyolGoEvents.Delivered => new(StatusMappingKind.NoOp, null, null, StatusNoOpReason.DeliveredByPlatform, null),
            TrendyolGoEvents.Picking or TrendyolGoEvents.Invoiced =>
                new(StatusMappingKind.NoOp, null, null, StatusNoOpReason.OwnOutboundStatusEcho, null),
            _ => new(StatusMappingKind.Unknown, null, null, null, new UnknownStatusEvidence(
                EvidenceId(externalOrderId, providerStatus), VocabularyVersion, externalOrderId, providerStatus, null))
        };
    }

    public Task<NormalizationResult> NormalizeAsync(string rawPayload, DateTimeOffset receivedAt, CancellationToken cancellationToken = default) =>
        _normalizer.NormalizeAsync(rawPayload, receivedAt, cancellationToken);

    public IReadOnlyList<OnlineOrderLineReference> ReadItemReferences(string rawPayload) =>
        TrendyolGoOrderNormalizer.ReadItemReferences(rawPayload);

    /// <summary>
    /// The handover follows the delivery kind recorded when the order was created: the platform's courier (<c>GO</c>)
    /// collects a prepared package; the restaurant's own courier (<c>STORE</c>) is dispatched. An in-store pickup has
    /// no documented courier step, so it is only reported prepared.
    /// </summary>
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
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) switch
        {
            TrendyolGoOrderNormalizer.PlatformCourier or TrendyolGoOrderNormalizer.StorePickup => OnlineOutboundStatus.ReadyForPickup,
            TrendyolGoOrderNormalizer.OwnCourier => OnlineOutboundStatus.Dispatched,
            _ => null
        };
    }

    public Task RequestStatusAsync(
        OnlineOrderStatusRequest request, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (action, reasonId) = request.Status switch
        {
            OnlineOutboundStatus.ReadyForPickup => (TrendyolGoPackageAction.Invoiced, (int?)null),
            OnlineOutboundStatus.Dispatched => (TrendyolGoPackageAction.InvoicedAndShipped, null),
            OnlineOutboundStatus.Cancelled => (TrendyolGoPackageAction.Unsupplied, TrendyolGoStatusSync.ReasonId(request.Reason)),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Status, "Unknown outbound status.")
        };
        return TrendyolGoStatusSync.EnqueueAsync(
            new TrendyolGoStatusUpdateRequested(Guid.NewGuid(), request.ExternalOrderId, action, reasonId, request.RequestedAt),
            connection, transaction, cancellationToken);
    }

    /// <summary>Trendyol Go cancels a package that is not accepted (reason 625), so acceptance is reported at once.</summary>
    public Task OrderAcceptedAsync(
        Guid orderId, string externalOrderId, NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default) =>
        TrendyolGoStatusSync.EnqueueAsync(
            new TrendyolGoStatusUpdateRequested(Guid.NewGuid(), externalOrderId, TrendyolGoPackageAction.Picked, null, DateTimeOffset.UtcNow),
            connection, transaction, cancellationToken);

    private static StatusMappingResult Cancel(CancellationParty party, string rawParty, string rawPayload)
    {
        string? reason = null;
        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("cancelInfo", out var info) && info.ValueKind == JsonValueKind.Object)
            {
                var code = TrendyolGoOrderNormalizer.Identifier(info, "reasonCode");
                var text = info.TryGetProperty("reason", out var reasonText) && reasonText.ValueKind == JsonValueKind.String
                    ? new string(reasonText.GetString()!.Where(c => !char.IsControl(c)).ToArray()).Trim()
                    : null;
                reason = string.Join(" ", new[] { code, text }.Where(part => !string.IsNullOrEmpty(part)));
                reason = reason.Length == 0 ? null : reason[..Math.Min(120, reason.Length)];
            }
        }
        catch (JsonException)
        {
            // The event is still a cancellation; only its reason is unreadable.
        }

        return new(StatusMappingKind.Command, InternalOrderCommand.CancelOrder, new CancellationDetail(party, rawParty, reason, false), null, null);
    }

    private static Guid EvidenceId(string externalOrderId, string providerStatus) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', VocabularyVersion, externalOrderId, providerStatus))).AsSpan(0, 16));
}
