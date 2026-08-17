using ALKAROS.TestHelpers;

namespace ALKAROS.Billing.SplitDesign.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V1-BIL-002 and applies catalog (006),
/// table_mgmt (010), orders (011), billing foundation (019), and split design (020) migrations in order.
/// </summary>
public sealed class SplitDesignTestDatabase : PgTestDatabase
{
    public SplitDesignTestDatabase()
        : base("alkaros_bil002_")
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
