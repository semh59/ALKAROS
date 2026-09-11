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
        // V1-RMD-113: submit-draft now dispatches a kitchen ticket exactly
        // like the terminal-wide quick-sale submit route, which has always
        // required this station id at the point an order is actually
        // submitted (DualScreenApplication.KitchenStationEnvironmentVariable).
        // Process-wide, but this test project runs in its own test host
        // process, so it does not leak into any other project's tests.
        Environment.SetEnvironmentVariable("ALKAROS_KITCHEN_STATION_ID", "grill-1");
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    /// <summary>V1-RMD-113: counts kitchen tickets dispatched for an order.</summary>
    public Task<long> KitchenTicketCountAsync(Guid orderId)
        => ScalarAsync<long>($"SELECT count(*) FROM kitchen.kitchen_tickets WHERE order_id = '{orderId:D}';");

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

    /// <summary>
    /// Seeds one purchasable catalog product and returns its id.
    /// <paramref name="isAvailable"/> defaults to true (matches the
    /// column's own DEFAULT); pass false to seed a manager-suspended
    /// ("86'd") product for V1-RMD-128 regression coverage.
    /// </summary>
    public async Task<Guid> SeedProductAsync(string name, decimal price, bool isAvailable = true)
    {
        var productId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active, is_available, current_price)
            VALUES (@product_id, @sku, @name, 1, 1, true, @is_available, @price);
            """,
            ("product_id", productId),
            ("sku", "ordtd-" + productId.ToString("N")[..8]),
            ("name", name),
            ("is_available", isAvailable),
            ("price", price));

        return productId;
    }

    /// <summary>
    /// V1-RMD-144: maps a product to a freshly seeded stock item holding
    /// <paramref name="onHandQuantity"/> units. Submitting a waiter order now
    /// consumes stock, and Semih's decision (2026-09-10) is that a product
    /// with no mapping at all refuses the whole submission — so every product
    /// a submit-draft test sends must be mapped. Returns the stock item id so
    /// a test can assert the balance afterwards.
    /// </summary>
    public async Task<Guid> SeedStockForProductAsync(Guid productId, decimal onHandQuantity)
    {
        var locationId = Guid.NewGuid();
        var stockItemId = Guid.NewGuid();
        var suffix = stockItemId.ToString("N")[..8];

        await ExecuteAsync(
            """
            INSERT INTO inventory.stock_locations (id, code, name, location_type)
            VALUES (@location_id, @location_code, 'Table Draft Test Location', 'Counter');
            INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, default_location_id)
            VALUES (@stock_item_id, @stock_item_code, 'Table Draft Test Stock Item', 'Portion', 'adet', @location_id);
            INSERT INTO inventory.product_stock_mappings (product_id, stock_item_id, quantity_multiplier)
            VALUES (@product_id, @stock_item_id, 1.0);
            INSERT INTO inventory.stock_balances (stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity, reserved_quantity, available_quantity)
            VALUES (@balance_id, @stock_item_id, @location_id, @on_hand, 0, @on_hand);
            """,
            ("location_id", locationId),
            ("location_code", "RMD144-" + suffix),
            ("stock_item_id", stockItemId),
            ("stock_item_code", "RMD144-" + suffix),
            ("product_id", productId),
            ("balance_id", Guid.NewGuid()),
            ("on_hand", onHandQuantity));

        return stockItemId;
    }

    /// <summary>Seeds a product already mapped to stock — the common case for a submit-draft test.</summary>
    public async Task<Guid> SeedStockedProductAsync(string name, decimal price, decimal onHandQuantity)
    {
        var productId = await SeedProductAsync(name, price);
        await SeedStockForProductAsync(productId, onHandQuantity);
        return productId;
    }

    /// <summary>
    /// V1-RMD-147: seeds a modifier group, assigns it to <paramref name="productId"/>
    /// and puts one modifier in it. Returns the modifier id. Pass
    /// <paramref name="assignToProduct"/> false to seed a modifier that exists but
    /// belongs to no group of this product — the "not yours" rejection case.
    /// </summary>
    public async Task<Guid> SeedModifierAsync(
        Guid productId, string name, decimal priceDelta, bool active = true, bool assignToProduct = true,
        int minSelections = 0, int maxSelections = 5)
    {
        var groupId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        var suffix = modifierId.ToString("N")[..8];

        await ExecuteAsync(
            """
            INSERT INTO catalog.modifier_groups (modifier_group_id, code, name, selection_type, min_selections, max_selections, active)
            VALUES (@group_id, @group_code, 'Table Draft Test Group', 2, @min_selections, @max_selections, true);
            INSERT INTO catalog.modifiers (modifier_id, modifier_group_id, code, name, price_delta, active)
            VALUES (@modifier_id, @group_id, @modifier_code, @name, @price_delta, @active);
            """,
            ("group_id", groupId),
            ("group_code", "RMD147G-" + suffix),
            ("min_selections", minSelections),
            ("max_selections", maxSelections),
            ("modifier_id", modifierId),
            ("modifier_code", "RMD147M-" + suffix),
            ("name", name),
            ("price_delta", priceDelta),
            ("active", active));

        if (assignToProduct)
        {
            await ExecuteAsync(
                """
                INSERT INTO catalog.product_modifier_groups (product_modifier_group_id, product_id, modifier_group_id)
                VALUES (@id, @product_id, @group_id);
                """,
                ("id", Guid.NewGuid()),
                ("product_id", productId),
                ("group_id", groupId));
        }

        return modifierId;
    }

    /// <summary>
    /// V1-RMD-161: like <see cref="SeedModifierAsync"/> but adds a SECOND
    /// modifier into the SAME group, so a test can select both to exercise
    /// a group's max_selections rule (a single-modifier group can never
    /// produce more than one selection to test against).
    /// </summary>
    public async Task<(Guid GroupId, Guid FirstModifierId, Guid SecondModifierId)> SeedModifierGroupWithTwoOptionsAsync(
        Guid productId, int minSelections, int maxSelections)
    {
        var groupId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var suffix = groupId.ToString("N")[..8];

        await ExecuteAsync(
            """
            INSERT INTO catalog.modifier_groups (modifier_group_id, code, name, selection_type, min_selections, max_selections, active)
            VALUES (@group_id, @group_code, 'Table Draft Test Group', 2, @min_selections, @max_selections, true);
            INSERT INTO catalog.modifiers (modifier_id, modifier_group_id, code, name, price_delta, active)
            VALUES (@first_id, @group_id, @first_code, 'Seçenek A', 10, true);
            INSERT INTO catalog.modifiers (modifier_id, modifier_group_id, code, name, price_delta, active)
            VALUES (@second_id, @group_id, @second_code, 'Seçenek B', 15, true);
            INSERT INTO catalog.product_modifier_groups (product_modifier_group_id, product_id, modifier_group_id)
            VALUES (@pmg_id, @product_id, @group_id);
            """,
            ("group_id", groupId),
            ("group_code", "RMD161G-" + suffix),
            ("min_selections", minSelections),
            ("max_selections", maxSelections),
            ("first_id", firstId),
            ("first_code", "RMD161A-" + suffix),
            ("second_id", secondId),
            ("second_code", "RMD161B-" + suffix),
            ("pmg_id", Guid.NewGuid()),
            ("product_id", productId));

        return (groupId, firstId, secondId);
    }

    /// <summary>
    /// V1-RMD-152: maps a modifier to its own freshly seeded stock item, so a
    /// test can assert that an extra really leaves the store room. Returns the
    /// stock item id.
    /// </summary>
    public async Task<Guid> SeedStockForModifierAsync(Guid modifierId, decimal onHandQuantity)
    {
        var locationId = Guid.NewGuid();
        var stockItemId = Guid.NewGuid();
        var suffix = stockItemId.ToString("N")[..8];

        await ExecuteAsync(
            """
            INSERT INTO inventory.stock_locations (id, code, name, location_type)
            VALUES (@location_id, @location_code, 'Modifier Stock Test Location', 'Counter');
            INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, default_location_id)
            VALUES (@stock_item_id, @stock_item_code, 'Modifier Stock Test Item', 'RawMaterial', 'adet', @location_id);
            INSERT INTO inventory.modifier_stock_mappings (modifier_id, stock_item_id, quantity_multiplier)
            VALUES (@modifier_id, @stock_item_id, 1.0);
            INSERT INTO inventory.stock_balances (stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity, reserved_quantity, available_quantity)
            VALUES (@balance_id, @stock_item_id, @location_id, @on_hand, 0, @on_hand);
            """,
            ("location_id", locationId),
            ("location_code", "RMD152-" + suffix),
            ("stock_item_id", stockItemId),
            ("stock_item_code", "RMD152-" + suffix),
            ("modifier_id", modifierId),
            ("balance_id", Guid.NewGuid()),
            ("on_hand", onHandQuantity));

        return stockItemId;
    }

    /// <summary>Current on-hand quantity of a stock item, for asserting a real decrement.</summary>
    /// <summary>V1-ORD-006: whether the table still points at this check.</summary>
    public Task<bool> TablePointsAtAsync(Guid tableId, Guid orderId)
        => ScalarAsync<bool>(
            $"SELECT EXISTS(SELECT 1 FROM table_mgmt.tables WHERE table_id = '{tableId:D}' AND current_order_id = '{orderId:D}');");

    /// <summary>V1-ORD-006: whether any check is attached to the table.</summary>
    public Task<bool> TableHasOpenCheckAsync(Guid tableId)
        => ScalarAsync<bool>(
            $"SELECT current_order_id IS NOT NULL FROM table_mgmt.tables WHERE table_id = '{tableId:D}';");

    /// <summary>V1-ORD-006: the table's own status column.</summary>
    public Task<string> TableStatusAsync(Guid tableId)
        => ScalarAsync<string>($"SELECT current_status FROM table_mgmt.tables WHERE table_id = '{tableId:D}';");

    /// <summary>V1-ORD-006: how many kitchen ticket lines exist for an order.</summary>
    public Task<long> KitchenTicketItemCountAsync(Guid orderId)
        => ScalarAsync<long>(
            $"""
            SELECT count(*)
            FROM kitchen.kitchen_ticket_items ti
            JOIN kitchen.kitchen_tickets t ON t.id = ti.ticket_id
            WHERE t.order_id = '{orderId:D}';
            """);

    public Task<decimal> OnHandQuantityAsync(Guid stockItemId)
        => ScalarAsync<decimal>($"SELECT on_hand_quantity FROM inventory.stock_balances WHERE stock_item_id = '{stockItemId:D}';");

    /// <summary>Counts the Consumption movements written against one order item.</summary>
    public Task<long> ConsumptionMovementCountAsync(Guid orderItemId)
        => ScalarAsync<long>(
            "SELECT count(*) FROM inventory.stock_movements " +
            $"WHERE source_type = 'Order' AND source_reference_id = '{orderItemId:D}' AND movement_type = 'Consumption';");
}
