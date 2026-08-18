using ALKAROS.TestHelpers;

namespace ALKAROS.Settings.TypedSettings.Tests.Fixtures;

/// <summary>
/// Isolated PostgreSQL database fixture with users, audit log and settings migrations (V1-SET-001).
/// </summary>
public sealed class SettingsTestDatabase : PgTestDatabase
{
    public SettingsTestDatabase()
        : base("alkaros_set001_")
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
