using ALKAROS.TestHelpers;

namespace ALKAROS.Identity.Authorization.Tests.Policies;

/// <summary>
/// A fresh identity database with the real migration chain applied through
/// V1-IAM-018 (044): 005, 008, 042, 043, 044. SQL is read from the repository
/// tree so the test exercises exactly what ships.
/// </summary>
public sealed class PolicyDatabase : PgTestDatabase
{
    private static readonly string RepoRoot = FindRepoRoot();

    public PolicyDatabase()
        : base("alkaros_iam018_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        string[] scripts =
        {
            Mig("V1-IAM-001", "005-users.up.sql"),
            Mig("V1-IAM-002", "008-identity-authorization.up.sql"),
            Mig("V1-RMD-097", "042-authorization-role-catalog.up.sql"),
            Mig("V1-IAM-017", "043-authorization-permission-split.up.sql"),
            Mig("V1-IAM-018", "044-authorization-policies.up.sql"),
        };

        foreach (var path in scripts)
            await RunAsync(DataSource, await File.ReadAllTextAsync(path));
    }

    public async Task ApplyDownAsync()
        => await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(Mig("V1-IAM-018", "044-authorization-policies.down.sql")));

    public async Task<bool> TableExistsAsync(string qualifiedName)
    {
        var parts = qualifiedName.Split('.', 2);
        await using var command = DataSource.CreateCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = @s AND table_name = @t);");
        command.Parameters.AddWithValue("s", parts[0]);
        command.Parameters.AddWithValue("t", parts[1]);
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
