namespace ALKAROS.Catalog.ProductCatalog;

/// <summary>
/// Represents a product in the catalog (PDF III.4.3).
/// Category, tax profile and description are optional; pricing arrives via
/// product_prices (V1-CAT-002), current_price is a nullable cache column.
/// </summary>
public sealed class Product
{
    public Product(
        Guid id,
        string sku,
        string name,
        ProductType productType,
        StockMode stockMode,
        Guid? categoryId = null,
        Guid? taxProfileId = null,
        string? description = null,
        string? printerRoutePolicy = null,
        int displayOrder = 0,
        decimal? currentPrice = null,
        bool active = true)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("Product SKU cannot be empty.", nameof(sku));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name cannot be empty.", nameof(name));
        if (currentPrice is < 0)
            throw new ArgumentOutOfRangeException(nameof(currentPrice), "Current price cannot be negative.");

        Id = id;
        Sku = sku;
        Name = name;
        ProductType = productType;
        StockMode = stockMode;
        CategoryId = categoryId;
        TaxProfileId = taxProfileId;
        Description = description;
        PrinterRoutePolicy = printerRoutePolicy;
        DisplayOrder = displayOrder;
        CurrentPrice = currentPrice;
        Active = active;
    }

    public Guid Id { get; }
    public string Sku { get; }
    public string Name { get; }
    public ProductType ProductType { get; }
    public StockMode StockMode { get; }
    public Guid? CategoryId { get; }
    public Guid? TaxProfileId { get; }
    public string? Description { get; }
    public string? PrinterRoutePolicy { get; }
    public int DisplayOrder { get; }
    public decimal? CurrentPrice { get; }
    public bool Active { get; }
}
