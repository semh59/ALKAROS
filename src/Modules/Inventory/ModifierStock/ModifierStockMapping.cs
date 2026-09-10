namespace ALKAROS.Inventory.ModifierStock;

/// <summary>
/// V1-RMD-152: what one modifier draws from the store room. Deliberately the
/// same shape and arithmetic as
/// <see cref="ALKAROS.Inventory.StockMaster.ProductStockMapping"/> — one
/// modifier can draw on several stock items, each with its own multiplier —
/// but a separate type and table, because a modifier is not a
/// <c>catalog.products</c> row and cannot be mapped through the product one.
/// </summary>
public sealed class ModifierStockMapping
{
    public ModifierStockMapping(
        Guid modifierId,
        Guid stockItemId,
        decimal quantityMultiplier = 1.0m,
        string? notes = null,
        DateTimeOffset? createdAt = null)
    {
        if (modifierId == Guid.Empty)
            throw new ArgumentException("ModifierId cannot be empty.", nameof(modifierId));

        if (stockItemId == Guid.Empty)
            throw new ArgumentException("StockItemId cannot be empty.", nameof(stockItemId));

        if (quantityMultiplier <= 0m)
            throw new ArgumentOutOfRangeException(
                nameof(quantityMultiplier),
                quantityMultiplier,
                "Quantity multiplier must be strictly greater than zero.");

        ModifierId = modifierId;
        StockItemId = stockItemId;
        QuantityMultiplier = quantityMultiplier;
        Notes = notes?.Trim();
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    public Guid ModifierId { get; }
    public Guid StockItemId { get; }
    public decimal QuantityMultiplier { get; }
    public string? Notes { get; }
    public DateTimeOffset CreatedAt { get; }
}
