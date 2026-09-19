using System.Text.Json;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.Observability.Tests;

/// <summary>
/// Applies the full runtime migration manifest — observability wiring pulls
/// in the identity permission catalog (reports.view, observability.manage
/// migration 135). Two tokens: a supervisor who holds BOTH reports.view AND
/// observability.manage (can read and mutate), and a view-only supervisor
/// who holds only reports.view.
/// </summary>
public sealed class ObservabilityTestDatabase : PgTestDatabase
{
    public static readonly Guid SupervisorUserId = Guid.NewGuid();
    public static readonly Guid ViewOnlyUserId = Guid.NewGuid();
    public static readonly Guid DeniedUserId = Guid.NewGuid();
    public const string SupervisorToken = "observability-supervisor-test-token";
    public const string ViewOnlyToken = "observability-view-only-test-token";
    public const string DeniedToken = "observability-denied-test-token";

    public ObservabilityTestDatabase() : base("alkaros_rmd251_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var root = FindRepositoryRoot();
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(root, "database", "MigrationComposition", "order.json")));
        var migrationRoot = Path.Combine(root, "database", "migrations");
        foreach (var migration in manifest.RootElement.GetProperty("migrations").EnumerateArray())
        {
            var id = migration.GetProperty("id").GetString()
                ?? throw new InvalidOperationException("Migration ID is missing.");
            var files = Directory.GetFiles(migrationRoot, $"{id}-*.up.sql", SearchOption.AllDirectories);
            if (files.Length != 1)
                throw new InvalidOperationException($"Expected one migration script for {id}, found {files.Length}.");
            await RunAsync(DataSource, await File.ReadAllTextAsync(files[0]));
        }

        await RunAsync(
            DataSource,
            $$"""
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES
                ('{{SupervisorUserId:D}}', 'observability-supervisor-{{SupervisorUserId:N}}', 'unused', 'Observability Supervisor', TRUE),
                ('{{ViewOnlyUserId:D}}', 'observability-view-only-{{ViewOnlyUserId:N}}', 'unused', 'Observability View Only', TRUE),
                ('{{DeniedUserId:D}}', 'observability-denied-{{DeniedUserId:N}}', 'unused', 'Observability Denied', TRUE);

            INSERT INTO identity.roles (role_id, code, name)
            VALUES
                ('{{Guid.NewGuid():D}}', 'observability-supervisor-role-{{SupervisorUserId:N}}', 'Observability Supervisor Test Role'),
                ('{{Guid.NewGuid():D}}', 'observability-view-only-role-{{ViewOnlyUserId:N}}', 'Observability View Only Test Role');

            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            SELECT gen_random_uuid(), r.role_id, p.permission_id
            FROM identity.roles r, identity.permissions p
            WHERE r.code = 'observability-supervisor-role-{{SupervisorUserId:N}}' AND p.code IN ('reports.view', 'observability.manage');

            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            SELECT gen_random_uuid(), r.role_id, p.permission_id
            FROM identity.roles r, identity.permissions p
            WHERE r.code = 'observability-view-only-role-{{ViewOnlyUserId:N}}' AND p.code = 'reports.view';

            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            SELECT gen_random_uuid(), '{{SupervisorUserId:D}}', role_id
            FROM identity.roles WHERE code = 'observability-supervisor-role-{{SupervisorUserId:N}}';

            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            SELECT gen_random_uuid(), '{{ViewOnlyUserId:D}}', role_id
            FROM identity.roles WHERE code = 'observability-view-only-role-{{ViewOnlyUserId:N}}';

            INSERT INTO identity.device_sessions
                (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES
                ('{{Guid.NewGuid():D}}', '{{SupervisorUserId:D}}', 'supervisor:test',
                 '{{DeviceSessionToken.Hash(SupervisorToken)}}', now(), now() + interval '1 hour'),
                ('{{Guid.NewGuid():D}}', '{{ViewOnlyUserId:D}}', 'supervisor:test-view-only',
                 '{{DeviceSessionToken.Hash(ViewOnlyToken)}}', now(), now() + interval '1 hour'),
                ('{{Guid.NewGuid():D}}', '{{DeniedUserId:D}}', 'manager:test-denied',
                 '{{DeviceSessionToken.Hash(DeniedToken)}}', now(), now() + interval '1 hour');
            """);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "database", "MigrationComposition", "order.json")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
