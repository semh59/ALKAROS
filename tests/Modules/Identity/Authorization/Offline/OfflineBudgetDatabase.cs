using ALKAROS.TestHelpers;

namespace ALKAROS.Identity.Authorization.Tests.Offline;

/// <summary>Identity database with the real chain through V1-IAM-025 (050).</summary>
public sealed class OfflineBudgetDatabase : PgTestDatabase
{
    private static readonly string RepoRoot = FindRepoRoot();

    public OfflineBudgetDatabase() : base("alkaros_iam022_") { }

    protected override async Task ApplySqlAsync()
    {
        string[] scripts =
        {
            Mig("V1-IAM-001", "005-users.up.sql"),
            Mig("V1-IAM-002", "008-identity-authorization.up.sql"),
            Mig("V1-RMD-097", "042-authorization-role-catalog.up.sql"),
            Mig("V1-IAM-017", "043-authorization-permission-split.up.sql"),
            Mig("V1-IAM-018", "044-authorization-policies.up.sql"),
            Mig("V1-IAM-019", "045-authorization-grants.up.sql"),
            Mig("V1-IAM-021", "046-authorization-delegations.up.sql"),
            Mig("V1-IAM-022", "047-offline-authority-budget.up.sql"),
            Mig("V1-IAM-025", "050-offline-authority-reissue.up.sql"),
        };
        foreach (var path in scripts)
            await RunAsync(DataSource, await File.ReadAllTextAsync(path));
    }

    public async Task ApplyDownAsync()
    {
        await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(Mig("V1-IAM-025", "050-offline-authority-reissue.down.sql")));
        await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(Mig("V1-IAM-022", "047-offline-authority-budget.down.sql")));
    }

    public async Task<bool> RelationExistsAsync(string qualifiedName)
    {
        await using var command = DataSource.CreateCommand("SELECT to_regclass(@n) IS NOT NULL;");
        command.Parameters.AddWithValue("n", qualifiedName);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static string Mig(string task, string file)
        => Path.Combine(RepoRoot, "database", "migrations", "V1", task, file);

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "ALKAROS.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
