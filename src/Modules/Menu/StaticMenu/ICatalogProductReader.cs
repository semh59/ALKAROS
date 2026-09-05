namespace ALKAROS.Menu.StaticMenu;

public interface ICatalogProductReader
{
    Task<CatalogProductInfo?> GetProductAsync(Guid productId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, CatalogProductInfo>> GetProductsAsync(IEnumerable<Guid> productIds, CancellationToken ct = default);
}
