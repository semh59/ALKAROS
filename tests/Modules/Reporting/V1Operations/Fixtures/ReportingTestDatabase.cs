using ALKAROS.TestHelpers;

namespace ALKAROS.Reporting.V1Operations.Tests.Fixtures;

/// <summary>
/// Isolated PostgreSQL database fixture with 031-operational-reports migration (V1-RPT-001).
/// </summary>
public sealed class ReportingTestDatabase : PgTestDatabase
{
    public ReportingTestDatabase()
        : base("alkaros_rpt001_")
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
