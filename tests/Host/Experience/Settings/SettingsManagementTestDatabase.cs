using System.Text.Json;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.Settings.Tests;

/// <summary>
/// Applies the full runtime migration manifest — Settings management pulls
/// in the identity permission catalog (settings.manage, migration 132).
/// </summary>
public sealed class SettingsManagementTestDatabase : PgTestDatabase
{
    public static readonly Guid ManagerUserId = Guid.NewGuid();
    public static readonly Guid DeniedUserId = Guid.NewGuid();
    public const string ManagerToken = "settings-manager-test-token";
    public const string DeniedToken = "settings-denied-test-token";

    public SettingsManagementTestDatabase() : base("alkaros_rmd246_")
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
                ('{{ManagerUserId:D}}', 'settings-manager-{{ManagerUserId:N}}', 'unused', 'Settings Manager', TRUE),
                ('{{DeniedUserId:D}}', 'settings-denied-{{DeniedUserId:N}}', 'unused', 'Settings Denied', TRUE);

            INSERT INTO identity.roles (role_id, code, name)
            VALUES ('{{Guid.NewGuid():D}}', 'settings-manager-role-{{ManagerUserId:N}}', 'Settings Manager Test Role');

            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            SELECT gen_random_uuid(), r.role_id, p.permission_id
            FROM identity.roles r, identity.permissions p
            WHERE r.code = 'settings-manager-role-{{ManagerUserId:N}}' AND p.code = 'settings.manage';

            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            SELECT gen_random_uuid(), '{{ManagerUserId:D}}', role_id
            FROM identity.roles WHERE code = 'settings-manager-role-{{ManagerUserId:N}}';

            INSERT INTO identity.device_sessions
                (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES
                ('{{Guid.NewGuid():D}}', '{{ManagerUserId:D}}', 'manager:test',
                 '{{DeviceSessionToken.Hash(ManagerToken)}}', now(), now() + interval '1 hour'),
                ('{{Guid.NewGuid():D}}', '{{DeniedUserId:D}}', 'manager:test-denied',
                 '{{DeviceSessionToken.Hash(DeniedToken)}}', now(), now() + interval '1 hour');
            """);
    }

    /// <summary>Seeds a real, active Boolean setting and returns its key.</summary>
    public async Task<string> SeedBooleanSettingAsync(string key, bool value, string moduleOwner = "TestModule")
    {
        await ExecuteAsync(
            """
            INSERT INTO settings.settings (setting_id, setting_key, setting_value, data_type, scope, module_owner)
            VALUES (@id, @key, @value, 'Boolean', 'Global', @owner);
            """,
            ("id", Guid.NewGuid()), ("key", key), ("value", value ? "true" : "false"), ("owner", moduleOwner));
        return key;
    }

    public async Task<string> GetRawValueAsync(string key)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT setting_value FROM settings.settings WHERE setting_key = @key;");
        command.Parameters.AddWithValue("key", key);
        return (string)(await command.ExecuteScalarAsync())!;
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
