using ALKAROS.TestHelpers;

namespace ALKAROS.Reconciliation.CaseFoundation.Tests.Fixtures;

/// <summary>
/// Isolated PostgreSQL database fixture with 030-reconciliation-cases migration (V1-REC-001).
/// </summary>
public sealed class ReconciliationTestDatabase : PgTestDatabase
{
    public ReconciliationTestDatabase()
        : base("alkaros_rec001_")
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
