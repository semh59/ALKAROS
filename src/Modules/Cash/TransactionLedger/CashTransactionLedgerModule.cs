using ALKAROS.ModuleComposition;

namespace ALKAROS.Cash.TransactionLedger;

/// <summary>
/// Registers the CashTransaction ledger (V13-CSH-002), separate from
/// `CashModule` (V1-CSH-001) and `CashSessionLifecycleModule` (V13-CSH-001)
/// so this task never has to write to either shared file.
/// </summary>
public sealed class CashTransactionLedgerModule : IModule
{
    public string Id => "Cash.TransactionLedger";
    public string DisplayName => "Cash Transaction Ledger";
    public IReadOnlyCollection<string> DependsOn => ["Cash"];

    public void Register(ModuleContext context)
        => context.RegisterTransient<ICashTransactionLedgerRepository, PostgresCashTransactionLedgerRepository>();
}
