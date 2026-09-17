using ALKAROS.TestHelpers;

namespace ALKAROS.Cash.SessionLifecycle.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V13-CSH-001 and applies the
/// cash_sessions/cash_counts migration (122). No other module's tables are
/// needed — CashSession has no foreign keys outside its own schema.
/// </summary>
public sealed class CashSessionTestDatabase : PgTestDatabase
{
    public CashSessionTestDatabase()
        : base("alkaros_csh001_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
        {
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
        }
    }
}
