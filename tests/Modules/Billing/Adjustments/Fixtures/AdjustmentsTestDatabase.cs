using ALKAROS.TestHelpers;

namespace ALKAROS.Billing.Adjustments.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V1-BIL-003 and applies catalog (006),
/// table_mgmt (010), orders (011), billing foundation (019), and bill adjustments (021) migrations.
/// </summary>
public sealed class AdjustmentsTestDatabase : PgTestDatabase
{
    public AdjustmentsTestDatabase()
        : base("alkaros_bil003_")
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
