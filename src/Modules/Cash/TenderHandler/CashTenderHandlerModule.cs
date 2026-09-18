using ALKAROS.ModuleComposition;

namespace ALKAROS.Cash.TenderHandler;

/// <summary>
/// Registers the cash tender handler (V13-CSH-003), separate from the other
/// Cash/Payments modules so this task never has to write to any of their
/// shared module files — same reasoning as
/// <see cref="TransactionLedger.CashTransactionLedgerModule"/>. Not yet
/// added to <c>ModuleRegistry.DefaultCatalog</c> — none of this task's own
/// dependencies (<c>CashTransactionLedgerModule</c>,
/// <c>PaymentAllocationPersistenceModule</c>) are registered there either;
/// wiring the full V1.3 composition into the live Host is V13-PAY-003's own
/// job (this task's Handoff).
/// </summary>
public sealed class CashTenderHandlerModule : IModule
{
    public string Id => "Cash.TenderHandler";
    public string DisplayName => "Cash Tender Handler";

    public IReadOnlyCollection<string> DependsOn =>
        ["Cash", "Cash.TransactionLedger", "Payments", "Payments.Allocations.Persistence"];

    public void Register(ModuleContext context)
        => context.RegisterTransient<ICashTenderHandler, CashTenderHandler>();
}
