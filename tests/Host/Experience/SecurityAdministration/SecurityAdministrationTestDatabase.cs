using System.Text.Json;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Secrets;
using ALKAROS.Security.DataProtectionRetention;
using ALKAROS.SensitiveData;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.SecurityAdministration.Tests;

/// <summary>
/// Applies the full runtime migration manifest (migration 142 seeds
/// security.manage for the manager role). Sessions: a manager holding
/// security.manage, a manager holding only reports.view, a supervisor-device
/// session whose user DOES hold security.manage (must still be refused: the
/// surface is manager-only), and a target user with two active sessions and a
/// lockout in progress.
/// </summary>
public sealed class SecurityAdministrationTestDatabase : PgTestDatabase
{
    public static readonly Guid ManagerUserId = Guid.NewGuid();
    public static readonly Guid ViewOnlyManagerUserId = Guid.NewGuid();
    public static readonly Guid SupervisorDeviceUserId = Guid.NewGuid();
    public static readonly Guid TargetUserId = Guid.NewGuid();
    public const string ManagerToken = "rmd266-manager-token";
    public const string ViewOnlyManagerToken = "rmd266-view-only-manager-token";
    public const string SupervisorDeviceToken = "rmd266-supervisor-device-token";

    public SecurityAdministrationTestDatabase() : base("alkaros_rmd266_")
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
                ('{{ManagerUserId:D}}', 'rmd266-manager-{{ManagerUserId:N}}', 'unused', 'Security Manager', TRUE),
                ('{{ViewOnlyManagerUserId:D}}', 'rmd266-view-only-{{ViewOnlyManagerUserId:N}}', 'unused', 'View Only Manager', TRUE),
                ('{{SupervisorDeviceUserId:D}}', 'rmd266-supervisor-{{SupervisorDeviceUserId:N}}', 'unused', 'Supervisor Device', TRUE);

            INSERT INTO identity.users (user_id, username, password_hash, display_name, active, failed_login_attempts, locked_until)
            VALUES ('{{TargetUserId:D}}', 'rmd266-target-{{TargetUserId:N}}', 'unused', 'Target User', TRUE, 5, now() + interval '30 minutes');

            INSERT INTO identity.roles (role_id, code, name)
            VALUES
                ('{{Guid.NewGuid():D}}', 'rmd266-manager-role-{{ManagerUserId:N}}', 'Security Manager Test Role'),
                ('{{Guid.NewGuid():D}}', 'rmd266-view-only-role-{{ViewOnlyManagerUserId:N}}', 'View Only Test Role');

            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            SELECT gen_random_uuid(), r.role_id, p.permission_id
            FROM identity.roles r, identity.permissions p
            WHERE r.code = 'rmd266-manager-role-{{ManagerUserId:N}}' AND p.code = 'security.manage';

            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            SELECT gen_random_uuid(), r.role_id, p.permission_id
            FROM identity.roles r, identity.permissions p
            WHERE r.code = 'rmd266-view-only-role-{{ViewOnlyManagerUserId:N}}' AND p.code = 'reports.view';

            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            SELECT gen_random_uuid(), '{{ManagerUserId:D}}', role_id
            FROM identity.roles WHERE code = 'rmd266-manager-role-{{ManagerUserId:N}}';

            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            SELECT gen_random_uuid(), '{{SupervisorDeviceUserId:D}}', role_id
            FROM identity.roles WHERE code = 'rmd266-manager-role-{{ManagerUserId:N}}';

            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            SELECT gen_random_uuid(), '{{ViewOnlyManagerUserId:D}}', role_id
            FROM identity.roles WHERE code = 'rmd266-view-only-role-{{ViewOnlyManagerUserId:N}}';

            INSERT INTO identity.device_sessions
                (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES
                ('{{Guid.NewGuid():D}}', '{{ManagerUserId:D}}', 'manager:test',
                 '{{DeviceSessionToken.Hash(ManagerToken)}}', now(), now() + interval '1 hour'),
                ('{{Guid.NewGuid():D}}', '{{ViewOnlyManagerUserId:D}}', 'manager:test-view-only',
                 '{{DeviceSessionToken.Hash(ViewOnlyManagerToken)}}', now(), now() + interval '1 hour'),
                ('{{Guid.NewGuid():D}}', '{{SupervisorDeviceUserId:D}}', 'supervisor:test',
                 '{{DeviceSessionToken.Hash(SupervisorDeviceToken)}}', now(), now() + interval '1 hour'),
                ('{{Guid.NewGuid():D}}', '{{TargetUserId:D}}', 'cashier:target-a',
                 '{{DeviceSessionToken.Hash("rmd266-target-a")}}', now(), now() + interval '1 hour'),
                ('{{Guid.NewGuid():D}}', '{{TargetUserId:D}}', 'waiter:target-b',
                 '{{DeviceSessionToken.Hash("rmd266-target-b")}}', now(), now() + interval '1 hour');
            """);
    }

    public Task<long> ActiveSessionCountAsync(Guid userId)
        => ScalarAsync<long>(
            $"SELECT count(*) FROM identity.device_sessions WHERE user_id = '{userId:D}' AND revoked_at IS NULL;");

    public Task<int> LockedAttemptsAsync(Guid userId)
        => ScalarAsync<int>(
            $"SELECT failed_login_attempts FROM identity.users WHERE user_id = '{userId:D}';");

    public Task<long> IsLockedAsync(Guid userId)
        => ScalarAsync<long>(
            $"SELECT count(*) FROM identity.users WHERE user_id = '{userId:D}' AND locked_until IS NOT NULL;");

    public Task<long> AuditCountAsync(string eventName, Guid aggregateId, Guid actorId)
        => ScalarAsync<long>(
            $"SELECT count(*) FROM audit.audit_events WHERE event_name = '{eventName}' AND aggregate_id = '{aggregateId:D}' AND actor_id = '{actorId:D}';");

    private static readonly SecretReference RetentionKey = new("Test/Rmd268Key");

    /// <summary>Seeds an expired, a legally held (also expired) and a fresh OrderNotes subject (5-year retention).</summary>
    public async Task<(Guid Expired, Guid Held, Guid Fresh)> SeedRetentionSubjectsAsync()
    {
        var secrets = new InMemorySecretProvider();
        secrets.Set(RetentionKey, "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=");
        var protector = new SensitivePayloadProtector(
            new AesGcmEnvelopeCipher(new SecretResolver(secrets, new AllowAllSecrets())),
            new AllowAllSensitive());
        SensitiveEnvelope Envelope() => protector.Protect(
            new SensitivePayload(
                new Dictionary<string, string> { ["value"] = "order-note" },
                new Dictionary<string, SensitiveCategory> { ["value"] = SensitiveCategory.Payment }),
            RetentionKey,
            "rmd268-test");

        var store = new PostgresRetentionSubjectStore(DataSource);
        var sixYearsAgo = DateTimeOffset.UtcNow.AddDays(-365 * 6);
        var expired = await store.InsertAsync(DataCategory.OrderNotes, Envelope(), false, sixYearsAgo, default);
        var held = await store.InsertAsync(DataCategory.OrderNotes, Envelope(), true, sixYearsAgo, default);
        var fresh = await store.InsertAsync(DataCategory.OrderNotes, Envelope(), false, DateTimeOffset.UtcNow, default);
        return (expired, held, fresh);
    }

    /// <summary>Restore-drill scratch databases still present on the server (must be 0 after a drill).</summary>
    public Task<long> ScratchDatabaseCountAsync()
        => ScalarAsync<long>("SELECT count(*) FROM pg_database WHERE datname LIKE 'alkaros_restore_drill_%';");

    public Task<long> DisposedCountAsync(Guid id)
        => ScalarAsync<long>($"SELECT count(*) FROM security.retention_subjects WHERE id = '{id:D}' AND disposed_at IS NOT NULL;");

    public Task<long> SystemAuditCountAsync(string eventName, Guid aggregateId)
        => ScalarAsync<long>(
            $"SELECT count(*) FROM audit.audit_events WHERE event_name = '{eventName}' AND aggregate_id = '{aggregateId:D}';");

    private sealed class AllowAllSecrets : ISecretAccessPolicy
    {
        public bool IsAllowed(string accessor, SecretReference reference) => true;
    }

    private sealed class AllowAllSensitive : ISensitiveDataAccessPolicy
    {
        public bool CanRead(string accessor, SensitiveEnvelope envelope) => true;
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
