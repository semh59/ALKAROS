namespace ALKAROS.Recipes.CatalogMapping;

/// <summary>
/// Links a catalog product to the recipe that explains what it consumes.
/// One recipe per product, unlike <c>Inventory.StockMaster.ProductStockMapping</c>
/// (which allows several stock items per product) — <see cref="ProductId"/>
/// alone is the key. The active <see cref="RecipeVersion"/> at the moment a
/// sale is accepted is always resolved fresh
/// (<c>IRecipeVersionRepository.GetActiveVersionAsync</c>), never cached
/// here, so a recipe revision takes effect immediately without touching
/// this mapping.
/// </summary>
public sealed class ProductRecipeMapping
{
    public ProductRecipeMapping(
        Guid productId,
        Guid recipeId,
        bool isActive = true,
        string? notes = null,
        DateTimeOffset? createdAt = null)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId cannot be empty.", nameof(productId));

        if (recipeId == Guid.Empty)
            throw new ArgumentException("RecipeId cannot be empty.", nameof(recipeId));

        ProductId = productId;
        RecipeId = recipeId;
        IsActive = isActive;
        Notes = notes?.Trim();
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    public Guid ProductId { get; }
    public Guid RecipeId { get; private set; }
    public bool IsActive { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    public void Update(Guid recipeId, bool isActive, string? notes)
    {
        if (recipeId == Guid.Empty)
            throw new ArgumentException("RecipeId cannot be empty.", nameof(recipeId));

        RecipeId = recipeId;
        IsActive = isActive;
        Notes = notes?.Trim();
    }
}
