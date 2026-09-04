using ALKAROS.TestHelpers;

namespace ALKAROS.Settings.KitchenLiveSync.Tests.Fixtures;

/// <summary>Isolated PostgreSQL database with users + settings migrations (V1-SET-002).</summary>
public sealed class KitchenLiveSyncTestDatabase : PgTestDatabase
{
    public KitchenLiveSyncTestDatabase()
        : base("alkaros_set002_")
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
