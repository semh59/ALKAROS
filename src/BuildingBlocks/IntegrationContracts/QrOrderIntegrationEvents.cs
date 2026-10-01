namespace ALKAROS.IntegrationContracts;

/// <summary>
/// V12-QRO-001: stable wire names for QR Ordering's integration events. Kept
/// in a sibling file to <see cref="IntegrationEventTypes"/> (Table
/// Management's registry) rather than added to it — each publisher owns its
/// own contract file (V0-ARC-001 §2), same convention as
/// <see cref="KitchenIntegrationEventTypes"/>.
/// </summary>
public static class QrOrderingIntegrationEventTypes
{
    public const string QrOrderSubmitted = "qr-ordering.order-submitted.v1";
}

/// <summary>
/// V12-QRO-001: QR Ordering publishes this after an authenticated QR
/// submission passes payload/price validation; Order consumes it to
/// materialize the actual `orders.orders` row (V0-ARC-001 row 19 — QR
/// Ordering has no direct-call edge to Order, only Identity and Table
/// Management; the Order interaction is event-only). The item snapshot
/// (name/price/tax) is taken by the QR Ordering module at submission time —
/// the consumer trusts it as-is rather than re-reading `catalog.products`,
/// exactly like a client-facing request is never trusted for pricing but a
/// server-computed snapshot is carried forward unchanged.
/// </summary>
public sealed record QrOrderSubmitted(
    Guid SubmissionId,
    Guid TableId,
    Guid CustomerSessionId,
    IReadOnlyList<QrOrderSubmittedItem> Items,
    DateTimeOffset SubmittedAt);

/// <summary>One line of a <see cref="QrOrderSubmitted"/> event's price/name snapshot.</summary>
public sealed record QrOrderSubmittedItem(
    Guid ItemId,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    string? Notes,
    IReadOnlyList<QrOrderSubmittedModifier>? Modifiers = null);

/// <summary>One extra chosen on a <see cref="QrOrderSubmittedItem"/>; name and price are the catalog's at submission time.</summary>
public sealed record QrOrderSubmittedModifier(Guid ModifierId, string Name, decimal PriceDelta, decimal Quantity);
