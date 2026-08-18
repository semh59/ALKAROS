using ALKAROS.TestHelpers;

namespace ALKAROS.Tables.TableTransfer.Tests.Fixtures;

/// <summary>
/// Creates a dedicated test PostgreSQL database and executes schema migrations up to 023-table-transfers.
/// </summary>
public sealed class TableTransferTestDatabase : PgTestDatabase
{
    public TableTransferTestDatabase()
        : base("alkaros_tbl002_")
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
