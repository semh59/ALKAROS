namespace ALKAROS.Inventory.StockMaster;

public interface IStockMasterService
{
    Task<StockLocation> CreateLocationAsync(
        string code,
        string name,
        StockLocationType locationType,
        bool isActive = true,
        CancellationToken ct = default);

    Task<StockItem> CreateStockItemAsync(
        string code,
        string name,
        StockItemType itemType,
        string trackingUnitCode,
        Guid? defaultLocationId = null,
        bool isActive = true,
        decimal? reorderPoint = null,
        CancellationToken ct = default);

    /// <summary>
    /// V11-INV-009: sets or clears (null) the on-hand threshold below which
    /// this item is flagged low. The future low-stock alert/report
    /// (V11-RPT-002) reads this rather than requiring a caller-supplied
    /// threshold every time.
    /// </summary>
    Task<StockItem> SetReorderPointAsync(
        Guid stockItemId,
        decimal? reorderPoint,
        CancellationToken ct = default);

    Task<ProductStockMapping> AssignProductToStockItemAsync(
        Guid productId,
        Guid stockItemId,
        decimal quantityMultiplier = 1.0m,
        string? notes = null,
        CancellationToken ct = default);

    Task<(StockItem Item, StockLocation Location)> ValidateMovementTargetAsync(
        Guid stockItemId,
        Guid stockLocationId,
        CancellationToken ct = default);
}
