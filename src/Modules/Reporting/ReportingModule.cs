namespace ALKAROS.Reporting;

using ALKAROS.ModuleComposition;
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
    }
}
