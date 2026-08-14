using ALKAROS.TestHelpers;

namespace ALKAROS.Tables.TableLifecycle.Tests.Fixtures;

/// <summary>
/// Creates a unique test database for the V1-TBL-001 table_mgmt schema and
/// applies the tables migration scripts (010-tables.up.sql).
/// </summary>
public sealed class TablesTestDatabase : PgTestDatabase
{
    public TablesTestDatabase()
        : base("alkaros_tbl001_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }
}