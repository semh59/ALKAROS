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

    /// <summary>Seeds one purchasable catalog product and returns its id.</summary>
    public async Task<Guid> SeedProductAsync(string name, decimal price, bool isAgeRestricted = false)
    {
        var productId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active, current_price, is_age_restricted)
            VALUES (@product_id, @sku, @name, 1, 1, true, @price, @is_age_restricted);
            """,
            ("product_id", productId),
            ("sku", "nfc-" + productId.ToString("N")[..8]),
            ("name", name),
            ("price", price),
            ("is_age_restricted", isAgeRestricted));

        return productId;
    }

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
