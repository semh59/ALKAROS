using ALKAROS.TestHelpers;

namespace ALKAROS.CustomerAccounts.AccountPayments.Tests.Fixtures;

/// <summary>Applies account_transactions (161, creates the schema) then account_payments (165).</summary>
public sealed class AccountPaymentTestDatabase : PgTestDatabase
{
    public AccountPaymentTestDatabase()
        : base("alkaros_acc004_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }
}
