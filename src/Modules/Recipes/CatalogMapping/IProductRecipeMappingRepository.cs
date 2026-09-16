namespace ALKAROS.Recipes.CatalogMapping;

public interface IProductRecipeMappingRepository
{
    Task AddOrUpdateAsync(ProductRecipeMapping mapping, CancellationToken ct = default);
    Task<ProductRecipeMapping?> GetByProductIdAsync(Guid productId, CancellationToken ct = default);

    /// <summary>
    /// The same row <see cref="GetByProductIdAsync"/> would return for each
    /// id, in one round trip — for a caller resolving several order items'
    /// mappings at once (V11-RCP-004's theoretical-consumption step).
    /// </summary>
    Task<IReadOnlyList<ProductRecipeMapping>> GetByProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);

    Task RemoveAsync(Guid productId, CancellationToken ct = default);
}
