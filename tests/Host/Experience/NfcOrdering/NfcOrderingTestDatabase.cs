using ALKAROS.TestHelpers;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.NfcOrdering.Tests;

/// <summary>
/// Isolated database with catalog, tables and orders — everything the
/// unauthenticated NFC ordering endpoint touches. No identity/authorization
/// tables: this surface has no session of any kind (V12-NFC-001).
/// </summary>
public sealed class NfcOrderingTestDatabase : PgTestDatabase
{
    public NfcOrderingTestDatabase()
        : base("alkaros_nfc_")
    {
        Environment.SetEnvironmentVariable("ALKAROS_KITCHEN_STATION_ID", "grill-1");
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    public Task<long> KitchenTicketCountAsync(Guid orderId)
        => ScalarAsync<long>($"SELECT count(*) FROM kitchen.kitchen_tickets WHERE order_id = '{orderId:D}';");

    /// <summary>Seeds a zone and a table with the given initial status and returns the table id.</summary>
    public async Task<Guid> SeedTableAsync(string status = "Available")
    {
        var zoneId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO table_mgmt.zones (zone_id, code, name) VALUES (@zone_id, @zone_code, 'Main Floor');
            INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, current_status)
            VALUES (@table_id, @zone_id, @table_number, 4, @status);
            """,
            ("zone_id", zoneId),
            ("zone_code", "ZONE-" + zoneId.ToString("N")[..8]),
            ("table_id", tableId),
            ("table_number", "T-" + tableId.ToString("N")[..6]),
            ("status", status));

        return tableId;
    }

    /// <summary>
    /// Seeds one purchasable catalog product and returns its id.
    /// <paramref name="isAvailable"/> defaults to true (matches the
    /// column's own DEFAULT); pass false to seed a manager-suspended
    /// ("86'd") product for V1-RMD-128 regression coverage.
    /// </summary>
    public async Task<Guid> SeedProductAsync(string name, decimal price, bool isAgeRestricted = false, bool isAvailable = true)
    {
        var productId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active, is_available, current_price, is_age_restricted)
            VALUES (@product_id, @sku, @name, 1, 1, true, @is_available, @price, @is_age_restricted);
            """,
            ("product_id", productId),
            ("sku", "nfc-" + productId.ToString("N")[..8]),
            ("name", name),
            ("is_available", isAvailable),
            ("price", price),
            ("is_age_restricted", isAgeRestricted));

        // V1-RMD-143: NFC's own trusted immediate-accept now also consumes
        // stock (OrderStockConsumptionService) — every seeded product needs
        // a real mapping with abundant on-hand quantity so existing
        // "the order reaches Accepted" tests keep representing that outcome
        // (a million units is comfortably above every quantity this suite
        // ever orders, including V1-RMD-138's own MaxQuantityPerItem tests).
        var locationId = Guid.NewGuid();
        var stockItemId = Guid.NewGuid();
        var suffix = stockItemId.ToString("N")[..8];
        await ExecuteAsync(
            """
            INSERT INTO inventory.stock_locations (id, code, name, location_type)
            VALUES (@location_id, @location_code, 'NFC Test Location', 'Counter');
            INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, default_location_id)
            VALUES (@stock_item_id, @stock_item_code, 'NFC Test Stock Item', 'Portion', 'adet', @location_id);
            INSERT INTO inventory.product_stock_mappings (product_id, stock_item_id, quantity_multiplier)
            VALUES (@product_id, @stock_item_id, 1.0);
            INSERT INTO inventory.stock_balances (stock_balance_id, stock_item_id, stock_location_id, on_hand_quantity, reserved_quantity, available_quantity)
            VALUES (@balance_id, @stock_item_id, @location_id, 1000000, 0, 1000000);
            """,
            ("location_id", locationId),
            ("location_code", "NFC-" + suffix),
            ("stock_item_id", stockItemId),
            ("stock_item_code", "NFC-" + suffix),
            ("product_id", productId),
            ("balance_id", Guid.NewGuid()));

        return productId;
    }

    /// <summary>Seeds an active modifier group with two options for <paramref name="productId"/> and returns the option ids.</summary>
    public async Task<(Guid First, Guid Second)> SeedModifierGroupAsync(Guid productId, int minSelections, int maxSelections)
    {
        var groupId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var suffix = groupId.ToString("N")[..8];
        await ExecuteAsync(
            """
            INSERT INTO catalog.modifier_groups (modifier_group_id, code, name, selection_type, min_selections, max_selections, active)
            VALUES (@group_id, @group_code, 'NFC Test Group', 2, @min_selections, @max_selections, true);
            INSERT INTO catalog.modifiers (modifier_id, modifier_group_id, code, name, price_delta, active)
            VALUES (@first_id, @group_id, @first_code, 'Ekstra peynir', 10, true);
            INSERT INTO catalog.modifiers (modifier_id, modifier_group_id, code, name, price_delta, active)
            VALUES (@second_id, @group_id, @second_code, 'Ekstra sos', 15, true);
            INSERT INTO catalog.product_modifier_groups (product_modifier_group_id, product_id, modifier_group_id)
            VALUES (@pmg_id, @product_id, @group_id);
            """,
            ("group_id", groupId),
            ("group_code", "NFCG-" + suffix),
            ("min_selections", minSelections),
            ("max_selections", maxSelections),
            ("first_id", firstId),
            ("first_code", "NFCA-" + suffix),
            ("second_id", secondId),
            ("second_code", "NFCB-" + suffix),
            ("pmg_id", Guid.NewGuid()),
            ("product_id", productId));
        return (firstId, secondId);
    }

    public Task<long> OrderCountAsync()
        => ScalarAsync<long>("SELECT count(*) FROM orders.orders;");

    public Task<long> ModifierRowCountAsync(Guid orderId)
        => ScalarAsync<long>(
            $"SELECT count(*) FROM orders.order_item_modifiers m JOIN orders.order_items i ON i.order_item_id = m.order_item_id WHERE i.order_id = '{orderId:D}';");

    public async Task<(string Status, Guid? CurrentOrderId, long RowVersion)> GetTableStateAsync(Guid tableId)
    {
        await using var cmd = DataSource.CreateCommand(
            "SELECT current_status, current_order_id, row_version FROM table_mgmt.tables WHERE table_id = @table_id;");
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetInt64(2));
    }
}
