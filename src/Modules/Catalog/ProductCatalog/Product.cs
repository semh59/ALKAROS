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
        bool active = true,
        bool isAvailable = true,
        long rowVersion = 1)
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
        IsAvailable = isAvailable;
        RowVersion = rowVersion;
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

    /// <summary>
    /// Whether the product is currently offered for sale. A suspended ("86'd")
    /// product stays in the catalog but is hidden from sales clients.
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// Optimistic concurrency token (catalog.products.row_version). Found
    /// missing by an independent audit (2026-09-05, B6): concurrent updates
    /// to the same product (e.g. two suspend/restore calls, or a suspend
    /// racing a future price edit) used to silently last-write-wins.
    /// </summary>
    public long RowVersion { get; }

    /// <summary>Hides the product from sales clients without deleting it.</summary>
    public Product Suspend() => IsAvailable ? WithAvailability(false) : this;

    /// <summary>Restores a suspended product to the sellable menu.</summary>
    public Product Restore() => IsAvailable ? this : WithAvailability(true);

    private Product WithAvailability(bool isAvailable) => new(
        Id, Sku, Name, ProductType, StockMode, CategoryId, TaxProfileId, Description,
        PrinterRoutePolicy, DisplayOrder, CurrentPrice, Active, isAvailable, RowVersion);
}
