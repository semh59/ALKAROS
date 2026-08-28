using ALKAROS.Identity.DeviceSessions;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.Catalog.Tests;

public sealed class CatalogApiTestDatabase : PgTestDatabase
{
    public static readonly Guid ManagerUserId = Guid.Parse("71000000-0000-0000-0000-000000000001");
    public static readonly Guid DeniedUserId = Guid.Parse("71000000-0000-0000-0000-000000000002");
    public const string ManagerToken = "catalog-manager-test-token";
    public const string DeniedToken = "catalog-denied-test-token";

    public CatalogApiTestDatabase() : base("alkaros_rmd014_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(path => path, StringComparer.Ordinal))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));

        await RunAsync(
            DataSource,
            $$"""
            INSERT INTO identity.users
                (user_id, username, password_hash, display_name, active)
            VALUES
                ('{{ManagerUserId:D}}', 'catalog-manager', 'unused', 'Catalog Manager', TRUE),
                ('{{DeniedUserId:D}}', 'catalog-denied', 'unused', 'Catalog Denied', TRUE);

            INSERT INTO identity.permissions (permission_id, code, name)
            VALUES ('71000000-0000-0000-0000-000000000003', 'catalog.manage', 'Manage catalog');

            INSERT INTO identity.roles (role_id, code, name)
            VALUES ('71000000-0000-0000-0000-000000000004', 'catalog-manager', 'Catalog Manager');

            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            VALUES (
                '71000000-0000-0000-0000-000000000005',
                '71000000-0000-0000-0000-000000000004',
                '71000000-0000-0000-0000-000000000003');

            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            VALUES (
                '71000000-0000-0000-0000-000000000006',
                '{{ManagerUserId:D}}',
                '71000000-0000-0000-0000-000000000004');

            INSERT INTO identity.device_sessions
                (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES
                (
                    '71000000-0000-0000-0000-000000000007',
                    '{{ManagerUserId:D}}',
                    'manager:test',
                    '{{DeviceSessionToken.Hash(ManagerToken)}}',
                    now(),
                    now() + interval '1 hour'),
                (
                    '71000000-0000-0000-0000-000000000008',
                    '{{DeniedUserId:D}}',
                    'manager:test-denied',
                    '{{DeviceSessionToken.Hash(DeniedToken)}}',
                    now(),
                    now() + interval '1 hour');
            """);
    }
}
