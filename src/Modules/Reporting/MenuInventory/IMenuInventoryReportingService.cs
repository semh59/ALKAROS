namespace ALKAROS.Reporting.MenuInventory;

public interface IMenuInventoryReportingService
{
    Task<PortionConsumptionReport> GetPortionConsumptionReportAsync(
        PortionConsumptionReportQuery query,
        CancellationToken ct = default);

    Task<ProductionYieldReport> GetProductionYieldReportAsync(
        ProductionYieldReportQuery query,
        CancellationToken ct = default);

    Task<WasteReport> GetWasteReportAsync(
        WasteReportQuery query,
        CancellationToken ct = default);

    Task<CriticalStockReport> GetCriticalStockReportAsync(
        CriticalStockReportQuery query,
        CancellationToken ct = default);
}
