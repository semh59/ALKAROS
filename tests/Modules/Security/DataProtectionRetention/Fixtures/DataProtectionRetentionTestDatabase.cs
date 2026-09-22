using ALKAROS.TestHelpers;

namespace ALKAROS.Security.DataProtectionRetention.Tests.Fixtures;

/// <summary>Real Postgres fixture for migration 137 (security.retention_subjects, V15-SEC-003).</summary>
public sealed class DataProtectionRetentionTestDatabase : PgTestDatabase
{
    public DataProtectionRetentionTestDatabase()
        : base("alkaros_sec003_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }
}
