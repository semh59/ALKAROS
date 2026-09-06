using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.Orders.TableDraft.Tests;

/// <summary>
/// Isolated database with users, identity/authorization, catalog, audit,
/// tables and orders — everything the table-draft and submit-draft
/// endpoints touch.
/// </summary>
public sealed class OrderManagementTableDraftTestDatabase : PgTestDatabase
{
    public OrderManagementTableDraftTestDatabase()
        : base("alkaros_ordtd_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    /// <summary>Seeds a cashier device session whose user holds orders.create and orders.send.</summary>
    public async Task<string> SeedCashierSessionAsync(Guid terminalId)
    {
        var userId = Guid.NewGuid();
        var suffix = userId.ToString("N");
        var (raw, hash) = DeviceSessionToken.Create();

        await ExecuteAsync(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Table Draft API Test', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            """,
            ("user_id", userId),
            ("username", "ordtd-api-" + suffix),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"cashier:{terminalId:D}"),
            ("token_hash", hash));

        var roleId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, 'Table Draft API Test Role');
            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            SELECT @role_permission_create, @role_id, permission_id FROM identity.permissions WHERE code = 'orders.create';
            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            SELECT @role_permission_send, @role_id, permission_id FROM identity.permissions WHERE code = 'orders.send';
            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            VALUES (@user_role_id, @user_id, @role_id);
            """,
            ("role_id", roleId),
            ("role_code", "ordtd-api-role-" + suffix),
            ("role_permission_create", Guid.NewGuid()),
            ("role_permission_send", Guid.NewGuid()),
            ("user_role_id", Guid.NewGuid()),
            ("user_id", userId));

        return $"{DualScreenApplication.CashierCookieName}={raw}";
    }

    /// <summary>
    /// V1-RMD-111: seeds a session under the given role, with the given
    /// permission codes granted outright, and returns the user id — needed
    /// for the garson-masa (ServingUserId / transfer-server) tests, which
    /// assert on who the acting user actually is, not just that a request
    /// succeeded. Mirrors OrderManagementCompTestDatabase's helper.
    /// </summary>
    public async Task<(Guid UserId, string Cookie)> SeedCashierSessionWithPermissionsAsync(
        Guid terminalId, string roleCode, params string[] permissionCodes)
    {
        var userId = Guid.NewGuid();
        var suffix = userId.ToString("N");
        var (raw, hash) = DeviceSessionToken.Create();

        await ExecuteAsync(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Table Draft API Test', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            """,
            ("user_id", userId),
            ("username", "ordtd-api-" + suffix),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"cashier:{terminalId:D}"),
            ("token_hash", hash));

        var roleId = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, 'Table Draft API Test Role');",
            ("role_id", roleId),
            ("role_code", roleCode + "-" + suffix));
        await ExecuteAsync(
            "INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES (@id, @user_id, @role_id);",
            ("id", Guid.NewGuid()),
            ("user_id", userId),
            ("role_id", roleId));

        foreach (var code in permissionCodes)
        {
            await ExecuteAsync(
                """
                INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
                SELECT @id, @role_id, permission_id FROM identity.permissions WHERE code = @code;
                """,
                ("id", Guid.NewGuid()),
                ("role_id", roleId),
                ("code", code));
        }

        return (userId, $"{DualScreenApplication.CashierCookieName}={raw}");
    }

    /// <summary>V1-RMD-111: reads the order's current serving_user_id column directly (no DTO exposes it).</summary>
    public async Task<Guid?> GetServingUserIdAsync(Guid orderId)
    {
        await using var cmd = DataSource.CreateCommand(
            "SELECT serving_user_id FROM orders.orders WHERE order_id = @order_id;");
        cmd.Parameters.Add("order_id", NpgsqlTypes.NpgsqlDbType.Uuid).Value = orderId;
        var result = await cmd.ExecuteScalarAsync();
        return result as Guid?;
    }

    /// <summary>Seeds a zone and a table (orders.orders.table_id has an FK to table_mgmt.tables) and returns the table id.</summary>
    public async Task<Guid> SeedTableAsync()
    {
        var zoneId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO table_mgmt.zones (zone_id, code, name) VALUES (@zone_id, @zone_code, 'Main Floor');
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, current_status)
            VALUES (@table_id, @zone_id, @table_number, 4, 'Available');
            """,
            ("zone_id", zoneId),
            ("zone_code", "ZONE-" + zoneId.ToString("N")[..8]),
            ("table_id", tableId),
            ("table_number", "T-" + tableId.ToString("N")[..6]));

        return tableId;
    }

    /// <summary>Seeds one purchasable catalog product and returns its id.</summary>
    public async Task<Guid> SeedProductAsync(string name, decimal price)
    {
        var productId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active, current_price)
            VALUES (@product_id, @sku, @name, 1, 1, true, @price);
            """,
            ("product_id", productId),
            ("sku", "ordtd-" + productId.ToString("N")[..8]),
            ("name", name),
            ("price", price));

        return productId;
    }
}
