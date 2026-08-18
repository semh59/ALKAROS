using ALKAROS.TestHelpers;

namespace ALKAROS.Tables.TableMerge.Tests.Fixtures;

/// <summary>
/// Creates a dedicated test PostgreSQL database and executes schema migrations up to 024-table-merges.
/// </summary>
public sealed class TableMergeTestDatabase : PgTestDatabase
{
    public TableMergeTestDatabase()
        : base("alkaros_tbl003_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        var upFiles = Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f).ToList();

        foreach (var file in upFiles)
        {
            var sql = await File.ReadAllTextAsync(file);
            await RunAsync(DataSource, sql);
        }
    }
}
