using ALKAROS.TestHelpers;

namespace ALKAROS.Identity.Authorization.Tests.Catalog;

/// <summary>
/// A fresh identity database with the real migration chain applied up to and
/// including V1-IAM-030 (112): base identity schema (005, 008), the V1-RMD-097
/// role catalog (042), the permission split (043), the drop of the
/// transitional <c>pos.cashier.mutate</c> alias (049), the manager admin
/// grants (054), transfer-server (055), integrations.manage (079), the
/// kitchen.advance / kitchen-staff split (109), the Kitchen-local
/// kitchen.availability.suspend permission (110, V1-KIT-008 — needed here
/// only so 111's role_permissions JOIN below has something to find, not
/// because it is itself part of ApplicationPermissions.Codes), the
/// kitchen-chef role (111, V1-IAM-029), and its kitchen.routing.manage
/// grant (112, V1-IAM-030), the cash.session.override supervisor-tier
/// permission (129, V1-RMD-236), the reports.close-day manager-tier
/// permission (133, V1-RMD-249), the reconciliation.manage supervisor-tier
/// permission (134, V1-RMD-250), and the observability.manage
/// supervisor-tier permission (135, V1-RMD-251), and the security.manage
/// manager-tier permission (142, V1-RMD-266). The SQL is read from the
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
            Mig("V1-IAM-024", "049-authorization-drop-mutate-alias.up.sql"),
            Mig("V1-RMD-110", "054-grant-identity-admin-permissions-to-manager.up.sql"),
            Mig("V1-RMD-111", "055-orders-transfer-server-permissions.up.sql"),
            MigVersioned("V12", "V12-QRT-003", "079-integrations-manage-permission.up.sql"),
            Mig("V1-IAM-028", "109-kitchen-advance-permission-and-staff-role.up.sql"),
            Mig("V1-KIT-008", "110-kitchen-availability-suspend-permission.up.sql"),
            Mig("V1-IAM-029", "111-kitchen-chef-role.up.sql"),
            Mig("V1-IAM-030", "112-kitchen-chef-routing-permission.up.sql"),
            Mig("V1-RMD-236", "129-cash-session-override-permission.up.sql"),
            Mig("V1-RMD-249", "133-reports-close-day-permission.up.sql"),
            Mig("V1-RMD-250", "134-reconciliation-manage-permission.up.sql"),
            Mig("V1-RMD-251", "135-observability-manage-permission.up.sql"),
            Mig("V1-RMD-266", "142-security-manage-permission.up.sql"),
        };

        foreach (var path in scripts)
            await RunAsync(DataSource, await File.ReadAllTextAsync(path));
    }

    /// <summary>
    /// Reverses everything applied after migration 042, in strict descending
    /// order: 112 down (drops the kitchen-chef routing grant), 111 down
    /// (drops the kitchen-chef role — its role_permissions rows, including
    /// 112's, cascade away regardless, 112 down runs first only to keep the
    /// same strict-descending convention every other reversal here follows),
    /// 110 down (drops kitchen.availability.suspend), 109 down (drops
    /// kitchen.advance and the kitchen-staff role), 079 down (drops
    /// integrations.manage), 055 down (drops the two transfer-server
    /// codes), 049 down (restores the alias), then 043 down (drops the 13
    /// granular codes and the <c>waiter</c> role). Migration 042's and
    /// 054's rows are left in place (054 grants identity.*.manage codes,
    /// which are not part of ApplicationPermissions.Codes and are not
    /// asserted here).
    /// </summary>
    public async Task ApplyDownSplitAsync()
    {
        await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(Mig("V1-RMD-266", "142-security-manage-permission.down.sql")));
        await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(Mig("V1-RMD-251", "135-observability-manage-permission.down.sql")));
        await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(Mig("V1-RMD-250", "134-reconciliation-manage-permission.down.sql")));
        await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(Mig("V1-RMD-249", "133-reports-close-day-permission.down.sql")));
        await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(Mig("V1-RMD-236", "129-cash-session-override-permission.down.sql")));
        await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(Mig("V1-IAM-030", "112-kitchen-chef-routing-permission.down.sql")));
        await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(Mig("V1-IAM-029", "111-kitchen-chef-role.down.sql")));
        await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(Mig("V1-KIT-008", "110-kitchen-availability-suspend-permission.down.sql")));
        await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(Mig("V1-IAM-028", "109-kitchen-advance-permission-and-staff-role.down.sql")));
        await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(MigVersioned("V12", "V12-QRT-003", "079-integrations-manage-permission.down.sql")));
        await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(Mig("V1-RMD-111", "055-orders-transfer-server-permissions.down.sql")));
        await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(Mig("V1-IAM-024", "049-authorization-drop-mutate-alias.down.sql")));
        await RunAsync(
            DataSource,
            await File.ReadAllTextAsync(Mig("V1-IAM-017", "043-authorization-permission-split.down.sql")));
    }

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
        => MigVersioned("V1", task, file);

    private static string MigVersioned(string version, string task, string file)
        => Path.Combine(RepoRoot, "database", "migrations", version, task, file);

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "ALKAROS.slnx")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
