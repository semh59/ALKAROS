namespace ALKAROS.Menu.DailyMenuLifecycle;

public interface ICatalogProductPriceReader
{
    Task<CatalogProductPriceInfo?> GetProductPriceInfoAsync(Guid productId, CancellationToken ct = default);
}
