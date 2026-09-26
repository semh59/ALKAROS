using System.Text.Json;
using ALKAROS.Catalog.ProductCatalog;
using ALKAROS.OnlineOrdering.Providers.Contracts;
using ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;

namespace ALKAROS.OnlineOrdering.Providers.TrendyolGo.OrderIntake;

/// <summary>
/// V12-TGO-002: turns a Trendyol Go package (the webhook <c>created</c> payload or a polled package; both carry the
/// same order fields) into internal order lines, all or nothing, through the shared product mapping under the
/// <c>trendyol-go</c> platform (a line's <c>productId</c> is its platform SKU).
///
/// UNVERIFIED DRAFT (EXT:TGO-MEAL-API, read 2026-09-27; V12-TGO-001 Blocked, C106 waiver):
/// <list type="bullet">
/// <item>the package <c>id</c> is the order identity; <c>orderCode</c> is what staff and the courier read;</item>
/// <item>a line's quantity is the number of its <c>items</c> that are not <c>isCancelled</c> (the document says the
/// product count is the count of <c>lineItemId</c>s); its <c>unitSellingPrice</c> (else <c>price</c>) is the unit
/// price, and in the document's sample the package <c>totalPrice</c> equals the lines' unit prices times their
/// items, so chosen modifiers are already inside it;</item>
/// <item>chosen modifiers, extra and removed ingredients have no catalog counterpart here: they reach the kitchen as
/// the line's instruction text;</item>
/// <item><c>deliveryType</c> <c>GO</c> is the platform's courier, <c>STORE</c> the restaurant's own; an in-store
/// pickup (<c>storePickupSelected</c>) is its own kind; anything else is refused;</item>
/// <item>the platform total is compared (never blocking) only when no coupon or promotion changes it:
/// <c>totalPrice</c> minus <c>totalDeliveryPrice</c>; how discounts enter <c>totalPrice</c> is not documented;</item>
/// <item>the customer's name, phone, address and meal-card payment stay only in the encrypted inbox payload.</item>
/// </list>
/// </summary>
public sealed class TrendyolGoOrderNormalizer
{
    public const string PlatformCourier = "GO";
    public const string OwnCourier = "STORE";
    public const string StorePickup = "STORE_PICKUP";

    private const int MaxIdentifierLength = 64;
    private const int MaxCommentLength = 200;
    private const int MaxInstructionsLength = 200;
    private const int MaxLines = 50;
    private const int MaxItemsPerLine = 999;
    private const decimal MaxUnitPrice = 1_000_000m;

    private readonly IYemeksepetiProductMappingService _mappings;
    private readonly IProductRepository _products;
    private readonly ITaxProfileRepository _taxProfiles;

    /// <param name="mappings">The shared mapping service reading <c>trendyol-go</c> rows.</param>
    public TrendyolGoOrderNormalizer(
        IYemeksepetiProductMappingService mappings, IProductRepository products, ITaxProfileRepository taxProfiles)
    {
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _taxProfiles = taxProfiles ?? throw new ArgumentNullException(nameof(taxProfiles));
    }

    public async Task<NormalizationResult> NormalizeAsync(string rawPayload, DateTimeOffset receivedAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawPayload);
        ParsedPackage parsed;
        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            if (TryParse(document.RootElement, out parsed) is { } refused)
                return refused;
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
                line.Sku, product.Id, product.Name, line.Quantity, line.UnitPrice, taxProfile.VatRate, line.Instructions, product.CurrentPrice));
        }

        return NormalizationResult.Accepted(new NormalizedOnlineOrder(
            parsed.PackageId, parsed.DisplayCode, parsed.TransportType, parsed.Comment, lines, parsed.ProviderSubTotal));
    }

    /// <summary>The platform SKUs and quantities of a package, read leniently (for a cancellation of an order never made locally).</summary>
    public static IReadOnlyList<OnlineOrderLineReference> ReadItemReferences(string rawPayload)
    {
        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            if (!document.RootElement.TryGetProperty("lines", out var lines) || lines.ValueKind != JsonValueKind.Array)
                return [];
            var references = new List<OnlineOrderLineReference>();
            foreach (var line in lines.EnumerateArray())
            {
                if (line.ValueKind == JsonValueKind.Object && Identifier(line, "productId") is { } sku && CountItems(line) is > 0 and var count)
                    references.Add(new OnlineOrderLineReference(sku, count));
            }

            return references;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static NormalizationResult? TryParse(JsonElement root, out ParsedPackage parsed)
    {
        parsed = default!;
        if (root.ValueKind != JsonValueKind.Object || Identifier(root, "id") is not { } packageId)
            return NormalizationResult.Rejected(NormalizationRejection.MalformedPayload, "id is required.");

        string transportType;
        if (root.TryGetProperty("storePickupSelected", out var pickup) && pickup.ValueKind == JsonValueKind.True)
            transportType = StorePickup;
        else if (Identifier(root, "deliveryType") is PlatformCourier or OwnCourier)
            transportType = Identifier(root, "deliveryType")!;
        else
            return NormalizationResult.Rejected(NormalizationRejection.UnsupportedTransportType, Identifier(root, "deliveryType"));

        var displayCode = Identifier(root, "orderCode") ?? Identifier(root, "orderNumber") ?? packageId[..Math.Min(8, packageId.Length)];
        var comment = CleanText(root, "customerNote", MaxCommentLength);

        if (!root.TryGetProperty("lines", out var linesElement) || linesElement.ValueKind != JsonValueKind.Array)
            return NormalizationResult.Rejected(NormalizationRejection.MalformedPayload, "lines is required.");
        if (linesElement.GetArrayLength() > MaxLines)
            return NormalizationResult.Rejected(NormalizationRejection.MalformedPayload, $"More than {MaxLines} lines.");

        var lines = new List<ParsedLine>();
        foreach (var line in linesElement.EnumerateArray())
        {
            if (line.ValueKind != JsonValueKind.Object || Identifier(line, "productId") is not { } sku)
                return NormalizationResult.Rejected(NormalizationRejection.MalformedPayload, "Every line needs a productId.");
            if (!line.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return NormalizationResult.Rejected(NormalizationRejection.MalformedPayload, "Every line needs items.");
            if (items.GetArrayLength() > MaxItemsPerLine)
                return NormalizationResult.Rejected(NormalizationRejection.InvalidQuantity, sku);
            var quantity = CountItems(line);
            if (quantity == 0)
                continue; // every item of the line was cancelled before it reached the restaurant

            if ((Price(line, "unitSellingPrice") ?? Price(line, "price")) is not { } unitPrice || unitPrice < 0m || unitPrice > MaxUnitPrice)
                return NormalizationResult.Rejected(NormalizationRejection.InvalidPrice, sku);

            lines.Add(new ParsedLine(sku, quantity, decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero), Instructions(line)));
        }

        if (lines.Count == 0)
            return NormalizationResult.Rejected(NormalizationRejection.EmptyOrder);

        parsed = new ParsedPackage(packageId, displayCode, transportType, comment, lines, ProviderSubTotal(root));
        return null;
    }

    private static decimal? ProviderSubTotal(JsonElement root)
    {
        if (HasDiscount(root))
            return null;
        if (Price(root, "totalPrice") is not { } total)
            return null;
        var delivery = Price(root, "totalDeliveryPrice") ?? 0m;
        return decimal.Round(total - delivery, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Whether any order- or item-level coupon or promotion is present with an amount other than zero.</summary>
    private static bool HasDiscount(JsonElement root)
    {
        if (NonZero(root, "coupon", "totalSellerAmount") || NonZeroArray(root, "promotions", "totalSellerAmount"))
            return true;
        if (!root.TryGetProperty("lines", out var lines) || lines.ValueKind != JsonValueKind.Array)
            return false;
        foreach (var line in lines.EnumerateArray())
        {
            if (!line.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object
                    && (item.TryGetProperty("coupon", out var coupon) && coupon.ValueKind == JsonValueKind.Object
                        || item.TryGetProperty("promotions", out var promotions) && promotions.ValueKind == JsonValueKind.Array && promotions.GetArrayLength() > 0))
                    return true;
            }
        }

        return false;

        static bool NonZero(JsonElement element, string objectName, string amountName) =>
            element.TryGetProperty(objectName, out var value) && value.ValueKind == JsonValueKind.Object
            && (Price(value, amountName) is not { } amount || amount != 0m);

        static bool NonZeroArray(JsonElement element, string arrayName, string amountName) =>
            element.TryGetProperty(arrayName, out var value) && value.ValueKind == JsonValueKind.Array
            && value.EnumerateArray().Any(entry => entry.ValueKind != JsonValueKind.Object || Price(entry, amountName) is not { } amount || amount != 0m);
    }

    private static int CountItems(JsonElement line) =>
        line.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray().Count(item => item.ValueKind == JsonValueKind.Object
                && !(item.TryGetProperty("isCancelled", out var cancelled) && cancelled.ValueKind == JsonValueKind.True))
            : 0;

    /// <summary>The kitchen's reading of a line's choices: modifiers (with their own choices), extras and removals.</summary>
    private static string? Instructions(JsonElement line)
    {
        var parts = new List<string>();
        var chosen = Names(line, "modifierProducts", nested: true);
        if (chosen.Count > 0)
            parts.Add("Seçimler: " + string.Join(", ", chosen));
        var extras = Names(line, "extraIngredients", nested: false);
        if (extras.Count > 0)
            parts.Add("Ekstra: " + string.Join(", ", extras));
        var removed = Names(line, "removedIngredients", nested: false);
        if (removed.Count > 0)
            parts.Add("Çıkarılacak: " + string.Join(", ", removed));
        if (parts.Count == 0)
            return null;
        var text = string.Join("; ", parts);
        return text.Length <= MaxInstructionsLength ? text : text[..(MaxInstructionsLength - 1)] + "…";
    }

    private static List<string> Names(JsonElement element, string arrayName, bool nested)
    {
        var names = new List<string>();
        if (!element.TryGetProperty(arrayName, out var array) || array.ValueKind != JsonValueKind.Array)
            return names;
        foreach (var entry in array.EnumerateArray())
        {
            var name = entry.ValueKind switch
            {
                JsonValueKind.String => Clean(entry.GetString()),
                JsonValueKind.Object => CleanText(entry, "name", 60),
                _ => null
            };
            if (name is null)
                continue;
            if (nested && entry.ValueKind == JsonValueKind.Object)
            {
                var inner = new List<string>();
                inner.AddRange(Names(entry, "modifierProducts", nested: true));
                inner.AddRange(Names(entry, "extraIngredients", nested: false).Select(n => "+" + n));
                inner.AddRange(Names(entry, "removedIngredients", nested: false).Select(n => "-" + n));
                if (inner.Count > 0)
                    name += " (" + string.Join(", ", inner) + ")";
            }

            names.Add(name);
        }

        return names;
    }

    private static decimal? Price(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var price)
            ? price
            : null;

    private static string? CleanText(JsonElement element, string name, int maxLength) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? Clean(value.GetString(), maxLength)
            : null;

    private static string? Clean(string? value, int maxLength = 60)
    {
        if (value is null)
            return null;
        var text = new string(value.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return text.Length == 0 ? null : text[..Math.Min(maxLength, text.Length)];
    }

    internal static string? Identifier(JsonElement element, string name)
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
        return string.IsNullOrEmpty(text) || text.Length > MaxIdentifierLength || text.Any(char.IsControl) ? null : text;
    }

    private sealed record ParsedLine(string Sku, decimal Quantity, decimal UnitPrice, string? Instructions);

    private sealed record ParsedPackage(
        string PackageId, string DisplayCode, string TransportType, string? Comment, IReadOnlyList<ParsedLine> Lines,
        decimal? ProviderSubTotal);
}
