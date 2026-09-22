using ALKAROS.TestHelpers;

namespace ALKAROS.Operations.OffsiteBackup.Tests.Fixtures;

/// <summary>Real Postgres fixture for migration 138 (operations.offsite_backup_receipts, V15-BKP-001).</summary>
public sealed class OffsiteBackupTestDatabase : PgTestDatabase
{
    public OffsiteBackupTestDatabase()
        : base("alkaros_bkp001_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }
}
