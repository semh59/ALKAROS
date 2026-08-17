using ALKAROS.TestHelpers;

namespace ALKAROS.Billing.BillFoundation.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V1-BIL-001 and applies catalog (006),
/// table_mgmt (010), orders (011) and billing (019) migrations in order.
/// </summary>
public sealed class BillingTestDatabase : PgTestDatabase
{
    public BillingTestDatabase()
        : base("alkaros_bil001_")
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
