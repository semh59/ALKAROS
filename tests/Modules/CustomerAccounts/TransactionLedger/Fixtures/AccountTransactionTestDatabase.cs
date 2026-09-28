using ALKAROS.TestHelpers;

namespace ALKAROS.CustomerAccounts.TransactionLedger.Tests.Fixtures;

public sealed class AccountTransactionTestDatabase : PgTestDatabase
{
    public AccountTransactionTestDatabase()
        : base("alkaros_acc001_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }
}
