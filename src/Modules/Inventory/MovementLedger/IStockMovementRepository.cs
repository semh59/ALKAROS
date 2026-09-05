namespace ALKAROS.Inventory.MovementLedger;

public interface IStockMovementRepository
{
    Task AppendAsync(StockMovement movement, CancellationToken ct = default);
    Task<StockMovement?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<StockMovement>> GetByStockItemAsync(Guid stockItemId, CancellationToken ct = default);
    Task<IReadOnlyList<StockMovement>> GetByLocationAsync(Guid stockLocationId, CancellationToken ct = default);
    Task<IReadOnlyList<StockMovement>> GetBySourceAsync(string sourceType, Guid sourceReferenceId, CancellationToken ct = default);
    Task<bool> HasReversalAsync(Guid stockMovementId, CancellationToken ct = default);
}
