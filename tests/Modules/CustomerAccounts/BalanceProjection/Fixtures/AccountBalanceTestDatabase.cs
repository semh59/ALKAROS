using ALKAROS.TestHelpers;

namespace ALKAROS.CustomerAccounts.BalanceProjection.Tests.Fixtures;

/// <summary>Applies account_transactions (161) then balances/balance_snapshots (162), in that order.</summary>
public sealed class AccountBalanceTestDatabase : PgTestDatabase
{
    public AccountBalanceTestDatabase()
        : base("alkaros_acc002_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }
}
