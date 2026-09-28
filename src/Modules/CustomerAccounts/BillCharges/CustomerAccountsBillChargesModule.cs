namespace ALKAROS.CustomerAccounts.BillCharges;

using ALKAROS.ModuleComposition;

/// <summary>
/// Registers the account charge handler (V14-ACC-003), separate from
/// <see cref="CustomerAccountsModule"/> so this task never has to write to
/// that module's own file - same reasoning as
/// `ALKAROS.Cash.TenderHandler.CashTenderHandlerModule`'s own doc comment.
/// Exercises module-dependency-rules.md row 16's pre-approved Bill/Payment
/// edges for the first time (V14-ACC-001/002 deliberately did not), plus a
/// new CustomerData edge for the eligibility check.
/// </summary>
public sealed class CustomerAccountsBillChargesModule : IModule
{
    public string Id => "CustomerAccounts.BillCharges";
    public string DisplayName => "Customer Accounts - Bill Charges";

    public IReadOnlyCollection<string> DependsOn =>
        ["CustomerAccounts", "CustomerData", "Payments", "Payments.Allocations.Persistence", "Billing"];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<ICustomerCreditPolicy, AlwaysApproveCreditPolicy>();
        context.RegisterTransient<IAccountChargeHandler, AccountChargeHandler>();
    }
}
