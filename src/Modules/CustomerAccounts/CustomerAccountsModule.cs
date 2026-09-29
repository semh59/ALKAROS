namespace ALKAROS.CustomerAccounts;

using ALKAROS.CustomerAccounts.AccountPayments;
using ALKAROS.CustomerAccounts.AccountReceipts;
using ALKAROS.CustomerAccounts.BalanceProjection;
using ALKAROS.CustomerAccounts.TransactionLedger;
using ALKAROS.ModuleComposition;

/// <summary>
/// V14-ACC-001/V14-ACC-002: the customer account transaction ledger and its
/// balance/snapshot projection (V0-DOM-007's own bounded context,
/// `docs/architecture/module-dependency-rules.md` row 16 - "Customer
/// Account"). The pre-approved Bill/Payment direct-call edges on that row
/// are not exercised here (no invoice generation, both tasks explicitly
/// leave that Out of scope) and will be exercised by a later task that
/// actually orchestrates a charge/payment against a real Bill or Payment
/// (e.g. V14-ACC-003 "bill-account-charge"), so this module declares no
/// module dependency yet.
/// </summary>
public sealed class CustomerAccountsModule : IModule
{
    public string Id => "CustomerAccounts";
    public string DisplayName => "Customer Accounts (Receivable Ledger)";
    public IReadOnlyCollection<string> DependsOn => [];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IAccountTransactionLedger, PostgresAccountTransactionLedger>();

        // V14-ACC-002: current_balance itself is kept up to date by the
        // database's own apply_transaction_to_balance trigger (migration
        // 162), atomic with every INSERT into account_transactions - none of
        // this registration writes to it.
        context.RegisterTransient<IAccountBalanceProjection, PostgresAccountBalanceProjection>();
        context.RegisterTransient<IBalanceSnapshotStore, PostgresBalanceSnapshotStore>();

        // V14-ACC-004: the bill-independent account payment aggregate; only
        // the method-specific receipt flows move it out of Requested.
        context.RegisterTransient<IAccountPaymentRepository, PostgresAccountPaymentRepository>();

        // V14-ACC-009: receipts for verified, bill-independent account payments.
        context.RegisterTransient<IAccountReceiptService, PostgresAccountReceiptService>();
    }
}
