namespace ALKAROS.Inventory.StockMaster;

public sealed class ProductStockMapping
{
    public ProductStockMapping(
        Guid productId,
        Guid stockItemId,
        decimal quantityMultiplier = 1.0m,
        string? notes = null,
        DateTimeOffset? createdAt = null)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId cannot be empty.", nameof(productId));

        if (stockItemId == Guid.Empty)
            throw new ArgumentException("StockItemId cannot be empty.", nameof(stockItemId));

        if (quantityMultiplier <= 0m)
            throw new InvalidProductStockMappingException(
                $"Quantity multiplier must be strictly greater than zero, got {quantityMultiplier}.");

        ProductId = productId;
        StockItemId = stockItemId;
        QuantityMultiplier = quantityMultiplier;
        Notes = notes?.Trim();
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    public Guid ProductId { get; }
    public Guid StockItemId { get; }
    public decimal QuantityMultiplier { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    public void UpdateMultiplier(decimal quantityMultiplier, string? notes)
    {
        if (quantityMultiplier <= 0m)
            throw new InvalidProductStockMappingException(
                $"Quantity multiplier must be strictly greater than zero, got {quantityMultiplier}.");

        QuantityMultiplier = quantityMultiplier;
        Notes = notes?.Trim();
    }
}
