using System.Text.Json;
using ALKAROS.Catalog.ProductCatalog;
using ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;

namespace ALKAROS.OnlineOrdering.Yemeksepeti.OrderNormalization;

/// <summary>
/// V12-ONL-002: turns a stored Yemeksepeti order payload into internal order lines, all or
/// nothing. Every SKU goes through the V12-MAP-001 mapping as of the moment the webhook
/// arrived; the first line that cannot be resolved rejects the whole order with a typed reason.
///
/// UNVERIFIED DRAFT (provider contract): the payload shape — <c>order_id</c>,
/// <c>external_order_id</c>, <c>order_code</c>, <c>transport_type</c>, <c>comment</c>,
/// <c>items[].sku</c>, <c>items[].pricing.pricing_type/quantity/unit_price</c>, and since V12-RMD-004
/// <c>items[].instructions/status/replaced_id</c> and <c>payment.sub_total</c> — is taken from the public
/// Partner API v2.0.2 order schema only; no real payload has been seen (V0-YSP-001 Blocked). The schema shows
/// <c>items[].status</c> only by the example <c>IN_CART</c>, so any other status, and any replaced item, is
/// refused for a person to handle rather than guessed at.
/// </summary>
public sealed class YemeksepetiOrderNormalizer
{
    private const int MaxIdentifierLength = 64;
    private const int MaxCommentLength = 200;
    private const int MaxInstructionsLength = 200;
    private const string DocumentedItemStatus = "IN_CART";
    private const int MaxLines = 50;
    private const decimal MaxQuantity = 999m;
    private const decimal MaxUnitPrice = 1_000_000m;

    private readonly IYemeksepetiProductMappingService _mappings;
    private readonly IProductRepository _products;
    private readonly ITaxProfileRepository _taxProfiles;

    public YemeksepetiOrderNormalizer(
        IYemeksepetiProductMappingService mappings,
        IProductRepository products,
        ITaxProfileRepository taxProfiles)
    {
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _taxProfiles = taxProfiles ?? throw new ArgumentNullException(nameof(taxProfiles));
    }

    public async Task<NormalizationResult> NormalizeAsync(
        string rawPayload,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawPayload);

        ParsedOrder parsed;
        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            var parseError = TryParse(document.RootElement, out parsed);
            if (parseError is not null)
                return parseError;
        }
        catch (JsonException)
        {
            return NormalizationResult.Rejected(NormalizationRejection.MalformedPayload, "Payload is not JSON.");
        }

        var lines = new List<NormalizedOnlineOrderLine>(parsed.Lines.Count);
        foreach (var line in parsed.Lines)
        {
            var resolution = await _mappings.ResolveAsync(line.Sku, receivedAt, cancellationToken).ConfigureAwait(false);
            var rejection = resolution.Outcome switch
            {
                ProductMappingResolutionOutcome.Resolved => (NormalizationRejection?)null,
                ProductMappingResolutionOutcome.Unmapped => NormalizationRejection.UnmappedSku,
                ProductMappingResolutionOutcome.Ambiguous => NormalizationRejection.AmbiguousSku,
                ProductMappingResolutionOutcome.ProductInactive => NormalizationRejection.ProductInactive,
                ProductMappingResolutionOutcome.ProductRequiresModifierChoice => NormalizationRejection.ProductRequiresModifierChoice,
                _ => throw new InvalidOperationException($"Unhandled mapping outcome '{resolution.Outcome}'.")
            };
            if (rejection is { } refused)
                return NormalizationResult.Rejected(refused, line.Sku);

            var product = await _products.GetByIdAsync(resolution.ProductId!.Value, cancellationToken).ConfigureAwait(false);
            if (product is null || !product.Active)
                return NormalizationResult.Rejected(NormalizationRejection.ProductInactive, line.Sku);
            if (product.TaxProfileId is not { } taxProfileId
                || await _taxProfiles.GetByIdAsync(taxProfileId, cancellationToken).ConfigureAwait(false) is not { } taxProfile)
                return NormalizationResult.Rejected(NormalizationRejection.ProductHasNoTaxProfile, line.Sku);

            lines.Add(new NormalizedOnlineOrderLine(
                line.Sku, product.Id, product.Name, line.Quantity, line.UnitPrice, taxProfile.VatRate, line.Instructions,
                product.CurrentPrice));
        }

        return NormalizationResult.Accepted(new NormalizedOnlineOrder(
            parsed.OrderId, parsed.DisplayCode, parsed.TransportType, parsed.Comment, lines, parsed.ProviderSubTotal));
    }

    private static NormalizationResult? TryParse(JsonElement root, out ParsedOrder parsed)
    {
        parsed = default!;
        if (root.ValueKind != JsonValueKind.Object
            || Identifier(root, "order_id") is not { } orderId
            || Identifier(root, "transport_type") is not { } transportType)
            return NormalizationResult.Rejected(NormalizationRejection.MalformedPayload, "order_id and transport_type are required.");
        if (YemeksepetiStatusSync.HandoverStatusFor(transportType) is null)
            return NormalizationResult.Rejected(NormalizationRejection.UnsupportedTransportType, transportType);

        var displayCode = Identifier(root, "external_order_id") ?? Identifier(root, "order_code") ?? orderId[..Math.Min(8, orderId.Length)];
        var comment = CleanText(root, "comment", MaxCommentLength);
        decimal? providerSubTotal = null;
        if (root.TryGetProperty("payment", out var payment) && payment.ValueKind == JsonValueKind.Object
            && payment.TryGetProperty("sub_total", out var subTotal) && subTotal.ValueKind == JsonValueKind.Number
            && subTotal.TryGetDecimal(out var subTotalValue))
            providerSubTotal = decimal.Round(subTotalValue, 2, MidpointRounding.AwayFromZero);

        if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return NormalizationResult.Rejected(NormalizationRejection.MalformedPayload, "items is required.");
        if (items.GetArrayLength() == 0)
            return NormalizationResult.Rejected(NormalizationRejection.EmptyOrder);
        if (items.GetArrayLength() > MaxLines)
            return NormalizationResult.Rejected(NormalizationRejection.MalformedPayload, $"More than {MaxLines} lines.");

        var lines = new List<ParsedLine>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || Identifier(item, "sku") is not { } sku
                || !item.TryGetProperty("pricing", out var pricing)
                || pricing.ValueKind != JsonValueKind.Object)
                return NormalizationResult.Rejected(NormalizationRejection.MalformedPayload, "Every item needs a sku and pricing.");

            if (item.TryGetProperty("status", out var status) && status.ValueKind != JsonValueKind.Null
                && (status.ValueKind != JsonValueKind.String || status.GetString() != DocumentedItemStatus))
                return NormalizationResult.Rejected(NormalizationRejection.UnsupportedItemStatus, sku);
            if (item.TryGetProperty("replaced_id", out var replaced) && replaced.ValueKind != JsonValueKind.Null
                && !(replaced.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(replaced.GetString())))
                return NormalizationResult.Rejected(NormalizationRejection.UnsupportedItemStatus, sku);
            if (Identifier(pricing, "pricing_type") != "UNIT")
                return NormalizationResult.Rejected(NormalizationRejection.UnsupportedPricingType, sku);
            if (!pricing.TryGetProperty("quantity", out var quantityElement)
                || quantityElement.ValueKind != JsonValueKind.Number
                || !quantityElement.TryGetDecimal(out var quantity)
                || quantity <= 0m || quantity > MaxQuantity || quantity != decimal.Truncate(quantity))
                return NormalizationResult.Rejected(NormalizationRejection.InvalidQuantity, sku);
            if (!pricing.TryGetProperty("unit_price", out var priceElement)
                || priceElement.ValueKind != JsonValueKind.Number
                || !priceElement.TryGetDecimal(out var unitPrice)
                || unitPrice < 0m || unitPrice > MaxUnitPrice)
                return NormalizationResult.Rejected(NormalizationRejection.InvalidPrice, sku);

            lines.Add(new ParsedLine(
                sku, quantity, decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero),
                CleanText(item, "instructions", MaxInstructionsLength)));
        }

        parsed = new ParsedOrder(orderId, displayCode, transportType, comment, lines, providerSubTotal);
        return null;
    }

    /// <summary>A free-text field with control characters removed, trimmed and bounded; null when absent or empty.</summary>
    private static string? CleanText(JsonElement element, string name, int maxLength)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        var text = new string(value.GetString()!.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return text.Length == 0 ? null : text[..Math.Min(maxLength, text.Length)];
    }

    private static string? Identifier(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return null;
        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
        text = text?.Trim();
        return string.IsNullOrEmpty(text) || text.Length > MaxIdentifierLength || text.Any(char.IsControl)
            ? null
            : text;
    }

    private sealed record ParsedLine(string Sku, decimal Quantity, decimal UnitPrice, string? Instructions);

    private sealed record ParsedOrder(
        string OrderId, string DisplayCode, string TransportType, string? Comment, IReadOnlyList<ParsedLine> Lines,
        decimal? ProviderSubTotal);
}
