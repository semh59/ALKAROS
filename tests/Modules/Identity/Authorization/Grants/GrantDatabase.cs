using ALKAROS.TestHelpers;

namespace ALKAROS.Identity.Authorization.Tests.Grants;

/// <summary>
/// A fresh identity database with the real migration chain through V1-IAM-019
/// (045): 005, 008, 042, 043, 044, 045.
/// </summary>
public sealed class GrantDatabase : PgTestDatabase
{
    private static readonly string RepoRoot = FindRepoRoot();

    public GrantDatabase() : base("alkaros_iam019_") { }

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
        };
        foreach (var path in scripts)
            await RunAsync(DataSource, await File.ReadAllTextAsync(path));
    }

    public async Task ApplyDownAsync()
        => await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(Mig("V1-IAM-019", "045-authorization-grants.down.sql")));

    public async Task<bool> RelationExistsAsync(string qualifiedName)
    {
        await using var command = DataSource.CreateCommand("SELECT to_regclass(@n) IS NOT NULL;");
        command.Parameters.AddWithValue("n", qualifiedName);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    public async Task<long> GrantCountAsync()
    {
        await using var command = DataSource.CreateCommand(
            "SELECT count(*) FROM identity.authorization_grants;");
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
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
