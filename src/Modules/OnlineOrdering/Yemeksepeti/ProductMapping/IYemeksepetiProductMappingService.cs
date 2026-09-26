namespace ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;

public interface IYemeksepetiProductMappingService
{
    /// <summary>
    /// From <paramref name="effectiveFrom"/> on, <paramref name="externalSku"/> stands for
    /// <paramref name="productId"/>. A SKU's previous open mapping ends at that moment; mapping
    /// the same SKU to the same product again returns the existing mapping. Throws
    /// <see cref="ProductMappingRejectedException"/> when the product cannot be sold through
    /// this channel or the change would rewrite history.
    /// </summary>
    Task<YemeksepetiProductMapping> MapAsync(
        string externalSku,
        Guid productId,
        DateTimeOffset effectiveFrom,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>V12-ONL-004: the SKU a product is currently published under, or null when it has no open mapping.</summary>
    Task<string?> FindOpenSkuForProductAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>What <paramref name="externalSku"/> meant at <paramref name="at"/> — the typed answer, never a guess.</summary>
    Task<ProductMappingResolution> ResolveAsync(
        string externalSku,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);
}
