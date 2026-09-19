using ALKAROS.ModuleComposition;

namespace ALKAROS.Cash.TenderHandler;

/// <summary>
/// Registers the cash tender handler (V13-CSH-003), separate from the other
/// Cash/Payments modules so this task never has to write to any of their
/// shared module files — same reasoning as
/// <see cref="TransactionLedger.CashTransactionLedgerModule"/>. Registered
/// in <c>ModuleRegistry.DefaultCatalog</c> alongside
/// <c>CashTransactionLedgerModule</c> and
/// <c>PaymentAllocationPersistenceModule</c> (wired by V13-CSH-004).
/// </summary>
public sealed class CashTenderHandlerModule : IModule
{
    public string Id => "Cash.TenderHandler";
    public string DisplayName => "Cash Tender Handler";

    public IReadOnlyCollection<string> DependsOn =>
        ["Cash", "Cash.TransactionLedger", "Payments", "Payments.Allocations.Persistence", "Billing"];

    public void Register(ModuleContext context)
        => context.RegisterTransient<ICashTenderHandler, CashTenderHandler>();
}
