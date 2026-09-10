namespace ALKAROS.Inventory.StockMaster;

public interface IStockItemRepository
{
    Task AddAsync(StockItem item, CancellationToken ct = default);
    Task<StockItem?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// V1-RMD-156: batched form of <see cref="GetByIdAsync"/> for a caller
    /// that already knows every id it wants — see
    /// IProductStockMappingRepository.GetByProductIdsAsync's own note.
    /// </summary>
    Task<IReadOnlyList<StockItem>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    Task<StockItem?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<StockItem>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<IReadOnlyList<StockItem>> GetByTypeAsync(StockItemType itemType, bool activeOnly = false, CancellationToken ct = default);
    Task UpdateAsync(StockItem item, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
