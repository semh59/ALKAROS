using ALKAROS.TestHelpers;

namespace ALKAROS.Observability.AlertFoundation.Tests.Fixtures;

/// <summary>
/// Isolated PostgreSQL database fixture with users and alerts migrations (V1-ALT-001).
/// </summary>
public sealed class AlertTestDatabase : PgTestDatabase
{
    public AlertTestDatabase()
        : base("alkaros_alt001_")
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
