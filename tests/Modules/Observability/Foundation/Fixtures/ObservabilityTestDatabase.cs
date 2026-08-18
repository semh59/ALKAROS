using ALKAROS.TestHelpers;

namespace ALKAROS.Observability.Foundation.Tests.Fixtures;

/// <summary>
/// Isolated PostgreSQL database fixture with health_checks migration (V1-OBS-001).
/// </summary>
public sealed class ObservabilityTestDatabase : PgTestDatabase
{
    public ObservabilityTestDatabase()
        : base("alkaros_obs001_")
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
