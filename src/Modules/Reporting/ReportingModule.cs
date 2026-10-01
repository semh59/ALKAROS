namespace ALKAROS.Reporting;

using ALKAROS.ModuleComposition;
using ALKAROS.Reporting.BusinessDayTotals;
using ALKAROS.Reporting.Channels;
using ALKAROS.Reporting.MenuInventory;
using ALKAROS.Reporting.Payments;
using ALKAROS.Reporting.ProductMargin;
using ALKAROS.Reporting.V1Operations;

public sealed class ReportingModule : IModule
{
    public string Id => "Reporting";
    public string DisplayName => "Operational Reporting";
    public IReadOnlyCollection<string> DependsOn => [];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IOperationalReportRepository, PostgresOperationalReportRepository>();
        context.RegisterTransient<IOperationalReportService, OperationalReportService>();
        // V1-RMD-421: the end-of-day close reads revenue and order count from recorded data.
        context.RegisterTransient<IBusinessDayTotalsReader, PostgresBusinessDayTotalsReader>();
        // V11-RPT-002: existed since V1.1 (PortionConsumption/ProductionYield/
        // Waste/CriticalStock reports) but had never been registered here or
        // given any Host endpoint — greenfield HTTP surface.
        context.RegisterTransient<IMenuInventoryReportingService, PostgresMenuInventoryReportingService>();
        // V13-RPT-001: payment/cash/reconciliation settlement report.
        context.RegisterTransient<IPaymentSettlementReportRepository, PostgresPaymentSettlementReportRepository>();
        context.RegisterTransient<IPaymentSettlementReportService, PaymentSettlementReportService>();
        // V12-RPT-001: QR/online channel report.
        context.RegisterTransient<IChannelReportService, PostgresChannelReportService>();
        context.RegisterTransient<IProductMarginReportService, PostgresProductMarginReportService>();
    }
}
