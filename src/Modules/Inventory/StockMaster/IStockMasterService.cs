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
