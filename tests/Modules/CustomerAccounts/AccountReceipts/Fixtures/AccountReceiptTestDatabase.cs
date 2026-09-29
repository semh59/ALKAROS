using ALKAROS.TestHelpers;

namespace ALKAROS.CustomerAccounts.AccountReceipts.Tests.Fixtures;

/// <summary>Applies account_transactions (161, creates the schema) then account_payments (165) and account_receipts (166).</summary>
public sealed class AccountReceiptTestDatabase : PgTestDatabase
{
    public AccountReceiptTestDatabase()
        : base("alkaros_acc009_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }
}
