using ALKAROS.TestHelpers;

namespace ALKAROS.Support.DiagnosticBundle.Tests.Fixtures;

/// <summary>
/// Real Postgres fixture applying the Audit (015) and Observability health
/// check (028) migrations this task's collaborators read/write - V15-SUP-001
/// owns no schema of its own, it only composes existing modules.
/// </summary>
public sealed class DiagnosticBundleTestDatabase : PgTestDatabase
{
    public DiagnosticBundleTestDatabase()
        : base("alkaros_sup001_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }
}
