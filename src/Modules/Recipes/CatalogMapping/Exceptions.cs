namespace ALKAROS.Recipes.CatalogMapping;

/// <summary>Thrown when no product-recipe mapping exists for the given product.</summary>
public sealed class ProductRecipeMappingNotFoundException : InvalidOperationException
{
    public ProductRecipeMappingNotFoundException(Guid productId)
        : base($"Product '{productId}' has no recipe mapping.")
    {
        ProductId = productId;
    }

    public Guid ProductId { get; }
}
