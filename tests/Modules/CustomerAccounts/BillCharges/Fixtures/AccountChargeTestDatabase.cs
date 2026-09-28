using ALKAROS.TestHelpers;

namespace ALKAROS.CustomerAccounts.BillCharges.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V14-ACC-003 and applies catalog,
/// tables, orders, billing, payments, payment_allocations,
/// customer_data.profiles and customer_account.account_transactions/
/// balances migrations in order - an account charge writes Payment,
/// PaymentAllocation AND AccountTransaction, and reads a real
/// CustomerProfile for eligibility. Mirrors
/// ALKAROS.Cash.TenderHandler.Tests.Fixtures.CashTenderHandlerTestDatabase's
/// own migration set, minus the cash-specific ones this task never touches.
/// </summary>
public sealed class AccountChargeTestDatabase : PgTestDatabase
{
    public AccountChargeTestDatabase()
        : base("alkaros_acc003_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }
}
