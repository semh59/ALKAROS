using ALKAROS.TestHelpers;

namespace ALKAROS.Operations.BackupHealth.Tests.Fixtures;

/// <summary>
/// Isolated PostgreSQL database fixture with 029-backup-health migration (V1-OPS-002).
/// </summary>
public sealed class BackupHealthTestDatabase : PgTestDatabase
{
    public BackupHealthTestDatabase()
        : base("alkaros_ops002_")
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
