using System.Text.Json;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.InventoryReporting.Tests;

/// <summary>
/// Applies the full runtime migration manifest, same as
/// <c>StockMasterTestDatabase</c>/<c>RecipeCatalogMappingTestDatabase</c> —
/// this endpoint needs the identity permission catalog (reports.view) plus
/// inventory.stock_items/stock_locations/stock_balances.
/// </summary>
public sealed class InventoryReportingTestDatabase : PgTestDatabase
{
    public static readonly Guid ViewerUserId = Guid.NewGuid();
    public static readonly Guid DeniedUserId = Guid.NewGuid();
    public const string ViewerToken = "inventory-reporting-viewer-test-token";
    public const string DeniedToken = "inventory-reporting-denied-test-token";

    public InventoryReportingTestDatabase() : base("alkaros_rpt002_")
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
                ('{{ViewerUserId:D}}', 'rpt002-viewer-{{ViewerUserId:N}}', 'unused', 'Reports Viewer', TRUE),
                ('{{DeniedUserId:D}}', 'rpt002-denied-{{DeniedUserId:N}}', 'unused', 'Reports Denied', TRUE);

            INSERT INTO identity.roles (role_id, code, name)
            VALUES ('{{Guid.NewGuid():D}}', 'rpt002-viewer-role-{{ViewerUserId:N}}', 'Reports Viewer Test Role');

            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            SELECT gen_random_uuid(), r.role_id, p.permission_id
            FROM identity.roles r, identity.permissions p
            WHERE r.code = 'rpt002-viewer-role-{{ViewerUserId:N}}' AND p.code = 'reports.view';

            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            SELECT gen_random_uuid(), '{{ViewerUserId:D}}', role_id
            FROM identity.roles WHERE code = 'rpt002-viewer-role-{{ViewerUserId:N}}';

            INSERT INTO identity.device_sessions
                (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES
                ('{{Guid.NewGuid():D}}', '{{ViewerUserId:D}}', 'manager:test',
                 '{{DeviceSessionToken.Hash(ViewerToken)}}', now(), now() + interval '1 hour'),
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

    public async Task<Guid> SeedStockItemAsync(string code, decimal? reorderPoint = null)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, reorder_point)
            VALUES (@id, @code, @code, 'RawMaterial', 'kg', @reorderPoint);
            """,
            ("id", id), ("code", code), ("reorderPoint", (object?)reorderPoint ?? DBNull.Value));
        return id;
    }

    public async Task SeedStockBalanceAsync(Guid stockItemId, Guid stockLocationId, decimal onHandQuantity)
    {
        await ExecuteAsync(
            """
            INSERT INTO inventory.stock_balances (stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity, available_quantity)
            VALUES (@id, @item, @loc, @qty, @qty);
            """,
            ("id", Guid.NewGuid()), ("item", stockItemId), ("loc", stockLocationId), ("qty", onHandQuantity));
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
