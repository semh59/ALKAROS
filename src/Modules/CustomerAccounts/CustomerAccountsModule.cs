namespace ALKAROS.CustomerAccounts;

using ALKAROS.CustomerAccounts.TransactionLedger;
using ALKAROS.ModuleComposition;

/// <summary>
/// V14-ACC-001: the customer account transaction ledger (V0-DOM-007's own
/// bounded context, `docs/architecture/module-dependency-rules.md` row 16 -
/// "Customer Account"). This task's own scope is only the append-only
/// ledger primitive itself; the pre-approved Bill/Payment direct-call edges
/// on that row are not exercised here (no cached balance, no invoice
/// generation - both explicitly Out of scope) and will be exercised by a
/// later task that actually orchestrates a charge/payment against a real
/// Bill or Payment (e.g. V14-ACC-003 "bill-account-charge"), so this module
/// declares no module dependency yet.
/// </summary>
public sealed class CustomerAccountsModule : IModule
{
    public string Id => "CustomerAccounts";
    public string DisplayName => "Customer Accounts (Receivable Ledger)";
    public IReadOnlyCollection<string> DependsOn => [];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IAccountTransactionLedger, PostgresAccountTransactionLedger>();
    }
}
