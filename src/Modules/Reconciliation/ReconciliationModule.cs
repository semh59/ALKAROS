namespace ALKAROS.Reconciliation;

using ALKAROS.ModuleComposition;
using ALKAROS.Reconciliation.CaseFoundation;

public sealed class ReconciliationModule : IModule
{
    public string Id => "Reconciliation";
    public string DisplayName => "Reconciliation Cases";
    public IReadOnlyCollection<string> DependsOn => [];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IReconciliationRepository, PostgresReconciliationRepository>();
        context.RegisterTransient<IReconciliationService, ReconciliationService>();
    }
}
