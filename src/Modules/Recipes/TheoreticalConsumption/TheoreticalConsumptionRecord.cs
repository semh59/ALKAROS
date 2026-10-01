namespace ALKAROS.Recipes.TheoreticalConsumption;

/// <summary>
/// V11-RCP-004: what a recipe SAYS an order item should have consumed,
/// computed from the active <c>RecipeVersion</c> at Accept time. An
/// append-only shadow ledger — it never touches
/// <c>inventory.stock_balances</c>, only feeds the future actual-vs-
/// theoretical variance report (V11-RPT-003). <see cref="StockItemId"/> is
/// the ingredient's own <c>IngredientItemId</c> (conceptually a StockItem
/// id, same convention <c>RecipeIngredientItem</c> already uses).
/// </summary>
public sealed class TheoreticalConsumptionRecord
{
    public TheoreticalConsumptionRecord(
        Guid id,
        Guid orderItemId,
        Guid productId,
        Guid recipeId,
        Guid recipeVersionId,
        Guid stockItemId,
        decimal quantity,
        string unitCode,
        DateTimeOffset? recordedAt = null)
        : this(id, orderItemId, productId, recipeId, recipeVersionId, null, stockItemId, quantity, unitCode, recordedAt)
    {
    }

    /// <summary>What an order-line extra mapped to a stock item should consume; it has no recipe.</summary>
    public static TheoreticalConsumptionRecord ForModifier(
        Guid id,
        Guid orderItemId,
        Guid productId,
        Guid modifierId,
        Guid stockItemId,
        decimal quantity,
        string unitCode,
        DateTimeOffset? recordedAt = null)
        => new(id, orderItemId, productId, null, null, modifierId, stockItemId, quantity, unitCode, recordedAt);

    private TheoreticalConsumptionRecord(
        Guid id,
        Guid orderItemId,
        Guid productId,
        Guid? recipeId,
        Guid? recipeVersionId,
        Guid? modifierId,
        Guid stockItemId,
        decimal quantity,
        string unitCode,
        DateTimeOffset? recordedAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (orderItemId == Guid.Empty)
            throw new ArgumentException("OrderItemId cannot be empty.", nameof(orderItemId));
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId cannot be empty.", nameof(productId));
        if (recipeId == Guid.Empty)
            throw new ArgumentException("RecipeId cannot be empty.", nameof(recipeId));
        if (recipeVersionId == Guid.Empty)
            throw new ArgumentException("RecipeVersionId cannot be empty.", nameof(recipeVersionId));
        if (modifierId == Guid.Empty)
            throw new ArgumentException("ModifierId cannot be empty.", nameof(modifierId));
        if (stockItemId == Guid.Empty)
            throw new ArgumentException("StockItemId cannot be empty.", nameof(stockItemId));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        if (string.IsNullOrWhiteSpace(unitCode))
            throw new ArgumentException("UnitCode cannot be empty.", nameof(unitCode));

        Id = id;
        OrderItemId = orderItemId;
        ProductId = productId;
        RecipeId = recipeId;
        RecipeVersionId = recipeVersionId;
        ModifierId = modifierId;
        StockItemId = stockItemId;
        Quantity = quantity;
        UnitCode = unitCode.Trim().ToLowerInvariant();
        RecordedAt = recordedAt ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }
    public Guid OrderItemId { get; }
    public Guid ProductId { get; }
    public Guid? RecipeId { get; }
    public Guid? RecipeVersionId { get; }
    public Guid? ModifierId { get; }
    public Guid StockItemId { get; }
    public decimal Quantity { get; }
    public string UnitCode { get; }
    public DateTimeOffset RecordedAt { get; }
}
