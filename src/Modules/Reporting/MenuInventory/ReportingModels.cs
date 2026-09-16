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

/// <summary>
/// V11-RPT-003: classic restaurant actual-vs-theoretical (AvT) variance.
/// <see cref="TheoreticalUsage"/> comes from V11-RCP-004's recipe-exploded
/// shadow ledger (<c>recipe.theoretical_consumption_records</c>) — that
/// ledger is item-level only, with no location column, so when the same
/// stock item is physically counted in more than one location, its
/// theoretical usage is attributed identically to each location's row
/// (a documented simplification, not a bug: most items live in exactly one
/// location). <see cref="ActualUsage"/> is
/// <c>OpeningCount + PurchaseReceipts - ClosingCount</c> — the classic
/// formula, computed from real <c>inventory.stock_physical_counts</c> rows,
/// never estimated.
/// </summary>
public sealed record ActualVsTheoreticalReportItem(
    Guid StockItemId,
    string StockItemCode,
    string StockItemName,
    string TrackingUnitCode,
    Guid StockLocationId,
    string LocationName,
    decimal OpeningCount,
    decimal ClosingCount,
    decimal PurchaseReceipts,
    decimal ActualUsage,
    decimal TheoreticalUsage,
    decimal VarianceQuantity,
    decimal? VariancePercentage);

public sealed record ActualVsTheoreticalReportQuery(
    DateTimeOffset FromTime,
    DateTimeOffset ToTime,
    Guid? LocationId = null);

/// <summary>
/// <paramref name="ExcludedForMissingCountsCount"/> is how many (stock
/// item, location) pairs had at least one physical count on record but
/// were still excluded from <paramref name="Items"/> because they were
/// missing an opening or closing count for THIS period — never estimated,
/// per the report's own "no false precision" rule.
/// </summary>
public sealed record ActualVsTheoreticalReport(
    IReadOnlyList<ActualVsTheoreticalReportItem> Items,
    int ExcludedForMissingCountsCount);
