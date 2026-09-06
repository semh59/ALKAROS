namespace ALKAROS.Reporting.MenuInventory;

public sealed record PortionConsumptionReportItem(
    Guid DailyMenuItemId,
    DateOnly BusinessDate,
    string ProductName,
    Guid? RecipeVersionId,
    decimal PlannedPortions,
    decimal PreparedPortions,
    decimal ReservedPortions,
    decimal ConsumedPortions,
    decimal WastePortions,
    decimal AvailablePortions,
    decimal SellThroughRate,
    bool IsReconciled);

public sealed record PortionConsumptionReportQuery(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    Guid? RecipeVersionId = null);

public sealed record PortionConsumptionReport(
    IReadOnlyList<PortionConsumptionReportItem> Items,
    decimal TotalPlannedPortions,
    decimal TotalPreparedPortions,
    decimal TotalConsumedPortions,
    decimal TotalWastePortions,
    decimal OverallSellThroughRate);

public sealed record ProductionYieldReportItem(
    Guid BatchId,
    string BatchNumber,
    Guid RecipeVersionId,
    string RecipeName,
    Guid? DestinationLocationId,
    string? DestinationLocationName,
    string Status,
    decimal PlannedQuantity,
    decimal ActualQuantity,
    decimal YieldRate,
    DateTimeOffset? ProducedAt,
    bool IsReconciled);

public sealed record ProductionYieldReportQuery(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    Guid? LocationId = null,
    Guid? RecipeVersionId = null);

public sealed record ProductionYieldReport(
    IReadOnlyList<ProductionYieldReportItem> Items,
    decimal TotalPlannedQuantity,
    decimal TotalActualQuantity,
    decimal OverallYieldRate);

public enum WasteReportCategory
{
    PortionWaste,
    InventoryWaste
}

public sealed record WasteReportItem(
    Guid WasteId,
    WasteReportCategory Category,
    Guid ItemId,
    string ItemName,
    Guid LocationId,
    string LocationName,
    decimal Quantity,
    string UnitCode,
    string? Reason,
    DateTimeOffset RecordedAt);

public sealed record WasteReportQuery(
    DateTimeOffset? FromTime = null,
    DateTimeOffset? ToTime = null,
    Guid? LocationId = null,
    WasteReportCategory? Category = null);

public sealed record WasteReport(
    IReadOnlyList<WasteReportItem> Items,
    int TotalWasteOccurrences,
    decimal TotalWastedQuantity);

public sealed record CriticalStockReportItem(
    Guid StockItemId,
    string StockItemCode,
    string StockItemName,
    string ItemType,
    string TrackingUnitCode,
    Guid StockLocationId,
    string LocationName,
    decimal OnHandQuantity,
    decimal ReservedQuantity,
    decimal AvailableQuantity,
    decimal CriticalThreshold,
    bool IsCritical,
    bool IsReconciled);

public sealed record CriticalStockReportQuery(
    Guid? LocationId = null,
    decimal? CriticalThreshold = null);

public sealed record CriticalStockReport(
    IReadOnlyList<CriticalStockReportItem> Items,
    int TotalCriticalItemsCount);
