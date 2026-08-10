namespace ALKAROS.Catalog.Pricing;

/// <summary>
/// Product price type discriminators. Values are defined by the V1-CAT-002
/// task contract (PDF III.4.4 "price_type"); the PDF declares the column but
/// does not enumerate values, so the canonical set is owned by this task
/// (canonical-value-catalog.md section D: price_type).
/// </summary>
public enum PriceType
{
    SalePrice = 1,
}

/// <summary>
/// A dated product price record (PDF III.4.4). Effective intervals are
/// half-open [effective_from, effective_to); nullable effective_to is an
/// open-ended price still in force. The database rejects overlapping periods
/// per (product, price_type, currency) so any timestamp resolves to at most
/// one price.
/// </summary>
public sealed class ProductPrice
{
    public ProductPrice(
        Guid id,
        Guid productId,
        PriceType priceType,
        decimal price,
        DateTimeOffset effectiveFrom,
        string currencyCode = "TRY",
        DateTimeOffset? effectiveTo = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Product price id cannot be empty.", nameof(id));
        if (productId == Guid.Empty)
            throw new ArgumentException("Product id cannot be empty.", nameof(productId));
        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3)
            throw new ArgumentException("Currency code must be a three-letter code.", nameof(currencyCode));
        if (effectiveTo is not null && effectiveTo <= effectiveFrom)
            throw new ArgumentOutOfRangeException(nameof(effectiveTo), "Effective end must be after effective start.");

        Id = id;
        ProductId = productId;
        PriceType = priceType;
        Price = price;
        CurrencyCode = currencyCode;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
    }

    public Guid Id { get; }
    public Guid ProductId { get; }
    public PriceType PriceType { get; }
    public decimal Price { get; }
    public string CurrencyCode { get; }
    public DateTimeOffset EffectiveFrom { get; }
    public DateTimeOffset? EffectiveTo { get; }
}