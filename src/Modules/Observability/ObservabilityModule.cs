namespace ALKAROS.Observability;

using ALKAROS.ModuleComposition;
using ALKAROS.Observability.AlertFoundation;
using ALKAROS.Observability.Foundation;
using ALKAROS.Observability.StructuredLogging;

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
        // V15-OBS-001: a fixed 1s/20-event window is a conservative default —
        // no caller has asked for a different bound yet, and sampling only
        // ever drops repeat emissions of the same event name, never the
        // caller's own work.
        context.RegisterSingleton<IEventSampler>(new FixedWindowEventSampler(TimeSpan.FromSeconds(1), 20));
        context.RegisterTransient<IStructuredEventLogger, StructuredEventLogger>();
    }
}
