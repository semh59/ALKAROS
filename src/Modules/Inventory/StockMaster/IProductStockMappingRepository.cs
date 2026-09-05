namespace ALKAROS.Inventory.StockMaster;

public interface IProductStockMappingRepository
{
    Task AddOrUpdateAsync(ProductStockMapping mapping, CancellationToken ct = default);
    Task<IReadOnlyList<ProductStockMapping>> GetByProductIdAsync(Guid productId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductStockMapping>> GetByStockItemIdAsync(Guid stockItemId, CancellationToken ct = default);
    Task RemoveAsync(Guid productId, Guid stockItemId, CancellationToken ct = default);
}
