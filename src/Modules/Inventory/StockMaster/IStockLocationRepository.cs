namespace ALKAROS.Inventory.StockMaster;

public interface IStockLocationRepository
{
    Task AddAsync(StockLocation location, CancellationToken ct = default);
    Task<StockLocation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<StockLocation?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<StockLocation>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default);
    Task UpdateAsync(StockLocation location, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
