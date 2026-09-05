namespace ALKAROS.Inventory.StockMaster;

public interface IStockItemRepository
{
    Task AddAsync(StockItem item, CancellationToken ct = default);
    Task<StockItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StockItem?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<StockItem>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<IReadOnlyList<StockItem>> GetByTypeAsync(StockItemType itemType, bool activeOnly = false, CancellationToken ct = default);
    Task UpdateAsync(StockItem item, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
