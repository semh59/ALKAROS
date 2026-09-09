using System.Text.Json;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.Purchasing.Tests;

/// <summary>
/// Applies the full runtime migration manifest (like
/// KitchenOperationsTestDatabase/MenuManagementTestDatabase) since
/// Purchasing pulls in inventory.stock_items/stock_locations and the
/// identity permission catalog.
/// </summary>
public sealed class PurchasingManagementTestDatabase : PgTestDatabase
{
    public static readonly Guid ManagerUserId = Guid.NewGuid();
    public static readonly Guid DeniedUserId = Guid.NewGuid();
    public const string ManagerToken = "purchasing-manager-test-token";
    public const string DeniedToken = "purchasing-denied-test-token";

    public PurchasingManagementTestDatabase() : base("alkaros_rmd132_")
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
                ('{{ManagerUserId:D}}', 'purchasing-manager-{{ManagerUserId:N}}', 'unused', 'Purchasing Manager', TRUE),
                ('{{DeniedUserId:D}}', 'purchasing-denied-{{DeniedUserId:N}}', 'unused', 'Purchasing Denied', TRUE);

            INSERT INTO identity.roles (role_id, code, name)
            VALUES ('{{Guid.NewGuid():D}}', 'purchasing-manager-role-{{ManagerUserId:N}}', 'Purchasing Manager Test Role');

            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            SELECT gen_random_uuid(), r.role_id, p.permission_id
            FROM identity.roles r, identity.permissions p
            WHERE r.code = 'purchasing-manager-role-{{ManagerUserId:N}}' AND p.code = 'purchasing.manage';

            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            SELECT gen_random_uuid(), '{{ManagerUserId:D}}', role_id
            FROM identity.roles WHERE code = 'purchasing-manager-role-{{ManagerUserId:N}}';

            INSERT INTO identity.device_sessions
                (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES
                ('{{Guid.NewGuid():D}}', '{{ManagerUserId:D}}', 'manager:test',
                 '{{DeviceSessionToken.Hash(ManagerToken)}}', now(), now() + interval '1 hour'),
                ('{{Guid.NewGuid():D}}', '{{DeniedUserId:D}}', 'manager:test-denied',
                 '{{DeviceSessionToken.Hash(DeniedToken)}}', now(), now() + interval '1 hour');
            """);
    }

    public async Task<Guid> SeedStockLocationAsync(string code)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO inventory.stock_locations (id, code, name, location_type) VALUES (@id, @code, @code, 'Warehouse');",
            ("id", id), ("code", code));
        return id;
    }

    public async Task<Guid> SeedStockItemAsync(string code, string trackingUnitCode = "kg")
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code) VALUES (@id, @code, @code, 'RawMaterial', @unit);",
            ("id", id), ("code", code), ("unit", trackingUnitCode));
        return id;
    }

    public async Task<decimal> GetOnHandQuantityAsync(Guid stockItemId, Guid stockLocationId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT on_hand_quantity FROM inventory.stock_balances WHERE stock_item_id = @item AND stock_location_id = @loc;");
        command.Parameters.AddWithValue("item", stockItemId);
        command.Parameters.AddWithValue("loc", stockLocationId);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? 0m : (decimal)result;
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
