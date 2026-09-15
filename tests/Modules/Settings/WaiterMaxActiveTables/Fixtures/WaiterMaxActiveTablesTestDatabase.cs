using ALKAROS.TestHelpers;

namespace ALKAROS.Settings.WaiterMaxActiveTables.Tests.Fixtures;

/// <summary>Isolated PostgreSQL database with users + settings migrations (V1-SET-006).</summary>
public sealed class WaiterMaxActiveTablesTestDatabase : PgTestDatabase
{
    public WaiterMaxActiveTablesTestDatabase()
        : base("alkaros_set006_")
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
