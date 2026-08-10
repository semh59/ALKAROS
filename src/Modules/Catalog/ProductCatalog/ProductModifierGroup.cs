namespace ALKAROS.Catalog.ProductCatalog;

/// <summary>
/// Links a product to a modifier group (PDF III.4.7). A product-modifier
/// group pair is unique.
/// </summary>
public sealed class ProductModifierGroup
{
    public ProductModifierGroup(Guid id, Guid productId, Guid modifierGroupId)
    {
        Id = id;
        ProductId = productId;
        ModifierGroupId = modifierGroupId;
    }

    public Guid Id { get; }
    public Guid ProductId { get; }
    public Guid ModifierGroupId { get; }
}
