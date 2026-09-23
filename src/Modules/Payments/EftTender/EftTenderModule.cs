using ALKAROS.ModuleComposition;

namespace ALKAROS.Payments.EftTender;

/// <summary>
/// Registers the EFT/Havale tender handler (V13-PAY-005), separate from the
/// other Payments modules so this task never has to write to any of their
/// shared module files — same reasoning as
/// <see cref="ALKAROS.Cash.TenderHandler.CashTenderHandlerModule"/> and
/// <see cref="ALKAROS.Payments.CardSettlement.CardSettlementModule"/>.
/// Registered in <c>ModuleRegistry.DefaultCatalog</c>.
/// </summary>
public sealed class EftTenderModule : IModule
{
    public string Id => "Payments.EftTender";
    public string DisplayName => "Payments EFT Tender";

    public IReadOnlyCollection<string> DependsOn =>
        ["Payments", "Payments.Allocations.Persistence", "Billing"];

    public void Register(ModuleContext context)
        => context.RegisterTransient<IEftTenderHandler, EftTenderHandler>();
}
