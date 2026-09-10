namespace ALKAROS.Inventory.StockMaster;

public interface IProductStockMappingRepository
{
    Task AddOrUpdateAsync(ProductStockMapping mapping, CancellationToken ct = default);
    Task<IReadOnlyList<ProductStockMapping>> GetByProductIdAsync(Guid productId, CancellationToken ct = default);

    /// <summary>
    /// V1-RMD-156: the same rows <see cref="GetByProductIdAsync"/> would
    /// return for each id, in one round trip. Written for
    /// OrderManagementStore.WithAvailableStockAsync, which used to call the
    /// single-id method once per order line — three queries per line, on the
    /// path every table-draft, submit and order read goes through.
    /// </summary>
    Task<IReadOnlyList<ProductStockMapping>> GetByProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);

    Task<IReadOnlyList<ProductStockMapping>> GetByStockItemIdAsync(Guid stockItemId, CancellationToken ct = default);
    Task RemoveAsync(Guid productId, Guid stockItemId, CancellationToken ct = default);
}
