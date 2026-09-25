namespace ALKAROS.OnlineOrdering.Yemeksepeti.OrderNormalization;

/// <summary>One order line resolved to an active catalog product.</summary>
public sealed record NormalizedOnlineOrderLine(
    string ExternalSku,
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate);

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
    IReadOnlyList<NormalizedOnlineOrderLine> Lines);

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
    ProductHasNoTaxProfile
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
