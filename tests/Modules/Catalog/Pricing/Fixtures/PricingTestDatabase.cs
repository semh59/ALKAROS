using ALKAROS.TestHelpers;

namespace ALKAROS.Catalog.Pricing.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V1-CAT-002 product pricing schema and
/// applies the catalog (006) and pricing (007) migration scripts in order.
/// </summary>
public sealed class PricingTestDatabase : PgTestDatabase
{
    public PricingTestDatabase()
        : base("alkaros_cat002_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }
}
