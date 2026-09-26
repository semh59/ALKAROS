using ALKAROS.IntegrationContracts;
using ALKAROS.Messaging;
using ALKAROS.OnlineOrdering.Providers.Contracts;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;
using Npgsql;

namespace ALKAROS.OnlineOrdering.Providers.TrendyolGo.StatusSync;

/// <summary>What the restaurant tells Trendyol Go about one package.</summary>
public enum TrendyolGoPackageAction
{
    /// <summary>Accepted, with the preparation time (<c>PUT .../packages/picked</c>).</summary>
    Picked,

    /// <summary>Prepared; the platform's courier collects it (<c>PUT .../packages/invoiced</c>).</summary>
    Invoiced,

    /// <summary>Prepared and sent with the restaurant's own courier (<c>invoiced</c>, then <c>.../manual-shipped</c>).</summary>
    InvoicedAndShipped,

    /// <summary>Cancelled by the restaurant (<c>PUT .../packages/unsupplied</c>, reason 621–627).</summary>
    Unsupplied
}

/// <summary>
/// A request to tell Trendyol Go about a local change, written to the outbox in the same transaction as the change.
/// <c>ExternalOrderId</c> is the package id (the name matches the other platforms' updates, which reconciliation reads).
/// </summary>
public sealed record TrendyolGoStatusUpdateRequested(
    Guid RequestId,
    string ExternalOrderId,
    TrendyolGoPackageAction Action,
    int? ReasonId,
    DateTimeOffset RequestedAt);

/// <summary>
/// V12-TGO-003. UNVERIFIED DRAFT (EXT:TGO-MEAL-API "Package Models", "Picked", "Invoiced", "Manual Shipped", "Cancel
/// Order", read 2026-09-27; V12-TGO-001 Blocked, C106 waiver).
/// </summary>
public static class TrendyolGoStatusSync
{
    public const string StatusUpdateRequestedEventType = "online-ordering.trendyol-go.status-update-requested.v1";

    /// <summary>The same provider retry budget as Yemeksepeti's updates (about three hours before an update is dead).</summary>
    public static readonly OutboxRetryProfile RetryProfile = YemeksepetiStatusSync.ProviderRetryProfile(StatusUpdateRequestedEventType);

    /// <summary>
    /// The document's restaurant cancellation reasons: 621 supply problem, 622 store closed, 623 the store cannot prepare
    /// the order (624 and 626 are only for restaurants with their own couriers; 627 is an order mix-up). A reason with no
    /// documented counterpart is never sent.
    /// </summary>
    public static int? ReasonId(OnlineCancellationReason? reason) => reason switch
    {
        OnlineCancellationReason.ItemUnavailable => 621,
        OnlineCancellationReason.Closed => 622,
        OnlineCancellationReason.TooBusy => 623,
        _ => null
    };

    public static Task EnqueueAsync(
        TrendyolGoStatusUpdateRequested request, NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExternalOrderId);
        if (request.Action == TrendyolGoPackageAction.Unsupplied && request.ReasonId is not (>= 621 and <= 627))
            throw new ArgumentException("A restaurant cancellation needs a documented reason (621-627).", nameof(request));

        return OutboxStore.EnqueueAsync(
            new OutboxEnvelope(
                StatusUpdateRequestedEventType, "trendyol_go_package", request.RequestId, IntegrationEventSerializer.Serialize(request)),
            connection, transaction, cancellationToken);
    }
}
