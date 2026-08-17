using ALKAROS.ModuleComposition;

namespace ALKAROS.Billing.BillFoundation;

/// <summary>
/// Composition module for the Billing bounded context (PDF:II.2.5).
/// Registers Billing repositories and services with the host composition root.
/// </summary>
public sealed class BillingModule : IModule
{
    public string Id => "Billing";

    public string DisplayName => "Billing";

    public IReadOnlyCollection<string> DependsOn => new[] { "Orders" };

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IBillRepository, PostgresBillRepository>();
    }
}
