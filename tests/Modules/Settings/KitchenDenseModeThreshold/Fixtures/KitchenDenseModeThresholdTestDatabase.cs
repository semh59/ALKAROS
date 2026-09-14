using ALKAROS.TestHelpers;

namespace ALKAROS.Settings.KitchenDenseModeThreshold.Tests.Fixtures;

/// <summary>Isolated PostgreSQL database with users + settings migrations (V1-SET-005).</summary>
public sealed class KitchenDenseModeThresholdTestDatabase : PgTestDatabase
{
    public KitchenDenseModeThresholdTestDatabase()
        : base("alkaros_set005_")
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
