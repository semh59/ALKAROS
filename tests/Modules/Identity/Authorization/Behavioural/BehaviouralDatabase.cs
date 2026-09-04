using ALKAROS.TestHelpers;

namespace ALKAROS.Identity.Authorization.Tests.Behavioural;

/// <summary>Identity database with the real chain through V1-IAM-023 (048).</summary>
public sealed class BehaviouralDatabase : PgTestDatabase
{
    private static readonly string RepoRoot = FindRepoRoot();

    public BehaviouralDatabase() : base("alkaros_iam023_") { }

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
            Mig("V1-IAM-023", "048-behavioural-tightening.up.sql"),
        };
        foreach (var path in scripts)
            await RunAsync(DataSource, await File.ReadAllTextAsync(path));
    }

    public async Task ApplyDownAsync()
        => await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(Mig("V1-IAM-023", "048-behavioural-tightening.down.sql")));

    public async Task<bool> RelationExistsAsync(string qualifiedName)
    {
        await using var command = DataSource.CreateCommand("SELECT to_regclass(@n) IS NOT NULL;");
        command.Parameters.AddWithValue("n", qualifiedName);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    /// <summary>Inserts a resolved <c>granted</c> grant so the rate source can count it.</summary>
    public async Task SeedGrantedAsync(
        Guid userId, string permissionCode, DateTimeOffset resolvedAt, string idempotencyKey)
    {
        await using var command = DataSource.CreateCommand(
            """
            INSERT INTO identity.authorization_grants
                (idempotency_key, permission_code, requester_user_id, requester_role_code,
                 amount, reason_code, requested_at, status, policy_path, resolved_at)
            VALUES (@key, @permission, @user, 'waiter', 0, 'CustomerChange', @resolved, 'granted', 'auto', @resolved);
            """);
        command.Parameters.AddWithValue("key", idempotencyKey);
        command.Parameters.AddWithValue("permission", permissionCode);
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("resolved", resolvedAt);
        await command.ExecuteNonQueryAsync();
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
