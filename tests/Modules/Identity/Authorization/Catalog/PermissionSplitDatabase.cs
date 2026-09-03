using ALKAROS.TestHelpers;

namespace ALKAROS.Identity.Authorization.Tests.Catalog;

/// <summary>
/// A fresh identity database with the real migration chain applied up to and
/// including V1-IAM-017 (043): base identity schema (005, 008), the V1-RMD-097
/// role catalog (042), then the permission split (043). The SQL is read from the
/// repository tree so the test exercises exactly what ships.
/// </summary>
public sealed class PermissionSplitDatabase : PgTestDatabase
{
    private static readonly string RepoRoot = FindRepoRoot();

    public PermissionSplitDatabase()
        : base("alkaros_iam017_")
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
        };

        foreach (var path in scripts)
            await RunAsync(DataSource, await File.ReadAllTextAsync(path));
    }

    public async Task ApplyDownSplitAsync()
        => await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(Mig("V1-IAM-017", "043-authorization-permission-split.down.sql")));

    public async Task<IReadOnlyList<string>> PermissionCodesForRoleAsync(string roleCode)
    {
        var codes = new List<string>();
        await using var command = DataSource.CreateCommand(
            """
            SELECT p.code
            FROM identity.roles r
            JOIN identity.role_permissions rp ON rp.role_id = r.role_id
            JOIN identity.permissions p ON p.permission_id = rp.permission_id
            WHERE r.code = @role
            ORDER BY p.code;
            """);
        command.Parameters.AddWithValue("role", roleCode);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            codes.Add(reader.GetString(0));
        return codes;
    }

    public async Task<long> RoleCountAsync(string roleCode)
        => await CountByCodeAsync("identity.roles", roleCode);

    public async Task<long> PermissionCountAsync(string code)
        => await CountByCodeAsync("identity.permissions", code);

    private async Task<long> CountByCodeAsync(string table, string code)
    {
        await using var command = DataSource.CreateCommand($"SELECT count(*) FROM {table} WHERE code = @c;");
        command.Parameters.AddWithValue("c", code);
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
