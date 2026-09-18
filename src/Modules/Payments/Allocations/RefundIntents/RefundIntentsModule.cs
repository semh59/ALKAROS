using ALKAROS.ModuleComposition;

namespace ALKAROS.Payments.Allocations.RefundIntents;

/// <summary>
/// Registers the RefundIntent repository (V13-ALC-003), separate from
/// `PaymentAllocationPersistenceModule` so this task never has to write to
/// that shared file — same reasoning as
/// <see cref="ALKAROS.Cash.TransactionLedger.CashTransactionLedgerModule"/>.
/// Not yet added to <c>ModuleRegistry.DefaultCatalog</c> — none of this
/// task's own dependencies are fully registered there either; wiring the
/// full V1.3 composition into the live Host is a later composition task's
/// own job (V13-ALC-004/V13-HUG-003).
/// </summary>
public sealed class RefundIntentsModule : IModule
{
    public string Id => "Payments.Allocations.RefundIntents";
    public string DisplayName => "Refund Intents";
    public IReadOnlyCollection<string> DependsOn => ["Payments", "Payments.Allocations.Persistence"];

    public void Register(ModuleContext context)
        => context.RegisterTransient<IRefundIntentRepository, PostgresRefundIntentRepository>();
}
