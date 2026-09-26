namespace ALKAROS.OnlineOrdering.Yemeksepeti.OrderNormalization;

/// <summary>
/// One order line resolved to an active catalog product. <see cref="Instructions"/> is the customer's note for
/// this item (a preparation instruction for the kitchen, PO:2026-09-01), control characters removed and bounded.
/// </summary>
public sealed record NormalizedOnlineOrderLine(
    string ExternalSku,
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    string? Instructions = null);

/// <summary>
/// A provider order in internal terms. <see cref="ExternalOrderId"/> is the provider's own
/// <c>order_id</c> and is the order's single external identity; <see cref="DisplayCode"/> is what
/// staff and the courier read off a screen. No customer name, phone or address is carried —
/// those stay in the encrypted webhook payload only.
/// </summary>
public sealed record NormalizedOnlineOrder(
    string ExternalOrderId,
    string DisplayCode,
    string TransportType,
    string? Comment,
    IReadOnlyList<NormalizedOnlineOrderLine> Lines,
    decimal? ProviderSubTotal = null)
{
    /// <summary>The lines' own total (unit price x quantity), what the local order is built from.</summary>
    public decimal LocalSubTotal => Lines.Sum(line => decimal.Round(line.UnitPrice * line.Quantity, 2, MidpointRounding.AwayFromZero));

    /// <summary>
    /// V12-RMD-004: whether the provider's <c>payment.sub_total</c> equals <see cref="LocalSubTotal"/>; null when
    /// the payload carried none. A mismatch never blocks the order; it is recorded and reconciled.
    /// </summary>
    public bool? TotalsMatch => ProviderSubTotal is { } provider ? provider == LocalSubTotal : null;
}

public enum NormalizationRejection
{
    MalformedPayload,
    EmptyOrder,

    /// <summary>A weight-priced (<c>KG</c>) or otherwise unknown pricing type; catalog products are sold by the unit.</summary>
    UnsupportedPricingType,

    InvalidQuantity,
    InvalidPrice,
    UnmappedSku,
    AmbiguousSku,
    ProductInactive,
    ProductRequiresModifierChoice,
    ProductHasNoTaxProfile,

    /// <summary>V12-RMD-004: a delivery kind no documented handover status exists for; it could never be handed over.</summary>
    UnsupportedTransportType,

    /// <summary>V12-RMD-004: an item replaced or in a state other than the documented <c>IN_CART</c> example.</summary>
    UnsupportedItemStatus
}

/// <summary>Either a normalized order or the first reason it cannot become one — never a partial order.</summary>
public sealed record NormalizationResult(
    NormalizedOnlineOrder? Order,
    NormalizationRejection? Rejection,
    string? Detail)
{
    public static NormalizationResult Accepted(NormalizedOnlineOrder order) => new(order, null, null);

    public static NormalizationResult Rejected(NormalizationRejection rejection, string? detail = null) =>
        new(null, rejection, detail);
}
