namespace ALKAROS.Reporting;

using ALKAROS.ModuleComposition;
using ALKAROS.Reporting.MenuInventory;
using ALKAROS.Reporting.Payments;
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
        // V11-RPT-002: existed since V1.1 (PortionConsumption/ProductionYield/
        // Waste/CriticalStock reports) but had never been registered here or
        // given any Host endpoint — greenfield HTTP surface.
        context.RegisterTransient<IMenuInventoryReportingService, PostgresMenuInventoryReportingService>();
        // V13-RPT-001: payment/cash/reconciliation settlement report.
        context.RegisterTransient<IPaymentSettlementReportRepository, PostgresPaymentSettlementReportRepository>();
        context.RegisterTransient<IPaymentSettlementReportService, PaymentSettlementReportService>();
    }
}
