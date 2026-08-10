using ALKAROS.TestHelpers;

namespace ALKAROS.Catalog.ProductCatalog.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for V1-CAT-001 catalog schema and applies
/// the catalog migration scripts.
/// </summary>
public sealed class CatalogTestDatabase : PgTestDatabase
{
    public CatalogTestDatabase()
        : base("alkaros_cat001_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }
}
