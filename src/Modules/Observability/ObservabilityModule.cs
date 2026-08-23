namespace ALKAROS.Observability;

using ALKAROS.ModuleComposition;
using ALKAROS.Observability.AlertFoundation;
using ALKAROS.Observability.Foundation;

public sealed class ObservabilityModule : IModule
{
    public string Id => "Observability";
    public string DisplayName => "Observability and Alerts";
    public IReadOnlyCollection<string> DependsOn => [];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IRedactionHook, ObservabilityRedactionHook>();
        context.RegisterTransient<IAlertRepository, PostgresAlertRepository>();
        context.RegisterTransient<IAlertService, AlertService>();
        context.RegisterTransient<IHealthCheckRepository, PostgresHealthCheckRepository>();
        context.RegisterTransient<IObservabilityService, ObservabilityService>();
    }
}
