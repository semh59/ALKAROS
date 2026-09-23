using ALKAROS.ModuleComposition;

namespace ALKAROS.Payments.CardSettlement;

/// <summary>
/// Registers card settlement orchestration (V13-PAY-004), separate from the
/// other Payments modules so this task never has to write to any of their
/// shared module files — same reasoning as
/// <see cref="ALKAROS.Cash.TenderHandler.CashTenderHandlerModule"/>.
/// Registered in <c>ModuleRegistry.DefaultCatalog</c>.
/// </summary>
public sealed class CardSettlementModule : IModule
{
    public string Id => "Payments.CardSettlement";
    public string DisplayName => "Payments Card Settlement";

    public IReadOnlyCollection<string> DependsOn =>
        ["Payments", "Payments.Allocations.Persistence", "Billing"];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<ICardSettlementAttemptRepository, PostgresCardSettlementAttemptRepository>();
        context.RegisterTransient<ICardSettlementOrchestrator, CardSettlementOrchestrator>();
    }
}
