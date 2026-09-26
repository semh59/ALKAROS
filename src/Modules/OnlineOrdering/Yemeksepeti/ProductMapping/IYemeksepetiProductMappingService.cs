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

    /// <summary>
    /// V12-RMD-005: like <see cref="MapAsync"/>, but when <paramref name="externalSku"/> already stands for another
    /// product nothing is changed and null is returned — decided under the same lock as the write, so a concurrent
    /// mapping can never slip in between the check and the write. Publishing uses this; it must never move a SKU.
    /// The default (for in-memory fakes) is the unlocked check-then-map; the Postgres service overrides it.
    /// </summary>
    async Task<YemeksepetiProductMapping?> MapIfUnownedAsync(
        string externalSku,
        Guid productId,
        DateTimeOffset effectiveFrom,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var current = await ResolveAsync(externalSku, effectiveFrom, cancellationToken).ConfigureAwait(false);
        if (current.ProductId is { } owner && owner != productId)
            return null;
        return await MapAsync(externalSku, productId, effectiveFrom, actorId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>V12-ONL-004: the SKU a product is currently published under, or null when it has no open mapping.</summary>
    Task<string?> FindOpenSkuForProductAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// V12-ONL-005: the currently open mappings (at most <paramref name="limit"/>), ordered by SKU. Since
    /// V12-RMD-005 only mappings already in effect: one that starts later is not published yet.
    /// </summary>
    Task<IReadOnlyList<YemeksepetiProductMapping>> ListOpenMappingsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>What <paramref name="externalSku"/> meant at <paramref name="at"/> — the typed answer, never a guess.</summary>
    Task<ProductMappingResolution> ResolveAsync(
        string externalSku,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);
}
