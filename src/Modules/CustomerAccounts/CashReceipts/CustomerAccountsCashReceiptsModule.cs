namespace ALKAROS.CustomerAccounts.CashReceipts;

using ALKAROS.ModuleComposition;

/// <summary>
/// V14-ACC-005: registers the cash account receipt handler, separate from <see cref="CustomerAccountsModule"/> for
/// the same reason as <c>CustomerAccountsBillChargesModule</c>. module-dependency-rules.md row 32: it writes the
/// customer's own ledger and account payment (Customer Accounts), checks the customer (Customer Data) and records the
/// cash movement (Cash.TransactionLedger).
/// </summary>
public sealed class CustomerAccountsCashReceiptsModule : IModule
{
    public string Id => "CustomerAccounts.CashReceipts";
    public string DisplayName => "Customer Accounts - Cash Receipts";

    public IReadOnlyCollection<string> DependsOn => ["CustomerAccounts", "CustomerData", "Cash.TransactionLedger"];

    public void Register(ModuleContext context)
        => context.RegisterTransient<ICashAccountReceiptHandler, CashAccountReceiptHandler>();
}
