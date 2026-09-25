using System.Text.Json;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.Recipes.Tests;

/// <summary>
/// Applies the full runtime migration manifest, same as
/// <c>StockMasterTestDatabase</c> (V1-RMD-143's own precedent) — this
/// endpoint set needs both <c>recipe.recipes</c> and
/// <c>recipe.product_recipe_mappings</c> plus the identity permission
/// catalog for real cookie-session auth.
/// </summary>
public sealed class RecipeCatalogMappingTestDatabase : PgTestDatabase
{
    public static readonly Guid ManagerUserId = Guid.NewGuid();
    public static readonly Guid DeniedUserId = Guid.NewGuid();
    public const string ManagerToken = "recipe-mapping-manager-test-token";
    public const string DeniedToken = "recipe-mapping-denied-test-token";

    public RecipeCatalogMappingTestDatabase() : base("alkaros_rcp003_")
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
                ('{{ManagerUserId:D}}', 'recipe-mapping-manager-{{ManagerUserId:N}}', 'unused', 'Recipe Mapping Manager', TRUE),
                ('{{DeniedUserId:D}}', 'recipe-mapping-denied-{{DeniedUserId:N}}', 'unused', 'Recipe Mapping Denied', TRUE);

            INSERT INTO identity.roles (role_id, code, name)
            VALUES ('{{Guid.NewGuid():D}}', 'recipe-mapping-manager-role-{{ManagerUserId:N}}', 'Recipe Mapping Manager Test Role');

            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            SELECT gen_random_uuid(), r.role_id, p.permission_id
            FROM identity.roles r, identity.permissions p
            WHERE r.code = 'recipe-mapping-manager-role-{{ManagerUserId:N}}' AND p.code = 'inventory.manage';

            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            SELECT gen_random_uuid(), '{{ManagerUserId:D}}', role_id
            FROM identity.roles WHERE code = 'recipe-mapping-manager-role-{{ManagerUserId:N}}';

            INSERT INTO identity.device_sessions
                (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES
                ('{{Guid.NewGuid():D}}', '{{ManagerUserId:D}}', 'manager:test',
                 '{{DeviceSessionToken.Hash(ManagerToken)}}', now(), now() + interval '1 hour'),
                ('{{Guid.NewGuid():D}}', '{{DeniedUserId:D}}', 'manager:test-denied',
                 '{{DeviceSessionToken.Hash(DeniedToken)}}', now(), now() + interval '1 hour');
            """);
    }

    public async Task<Guid> SeedStockItemAsync(string code, string trackingUnitCode = "kg")
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code) VALUES (@id, @code, @code, 'RawMaterial', @unit);",
            ("id", id), ("code", code), ("unit", trackingUnitCode));
        return id;
    }

    public async Task<Guid> SeedRecipeAsync(string code)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO recipe.recipes (id, code, name) VALUES (@id, @code, @code);",
            ("id", id), ("code", code));
        return id;
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
