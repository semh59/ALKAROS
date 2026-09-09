using ALKAROS.TestHelpers;

namespace ALKAROS.QrOrdering.PendingOrders.Tests.Fixtures;

/// <summary>
/// A table_mgmt + catalog + qr_ordering + outbox test database for
/// V12-QRO-001: QrPendingOrderStore.SubmitAsync exchanges a real table token
/// (V12-QRS-001) for a customer session (V12-QRS-003), resolves a real
/// catalog price snapshot, and enqueues through the real outbox.
/// </summary>
public sealed class QrOrderingPendingOrdersTestDatabase : PgTestDatabase
{
    public QrOrderingPendingOrdersTestDatabase()
        : base("alkaros_qro001_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

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
    /// <paramref name="isAvailable"/> defaults to true (matches the
    /// column's own DEFAULT); pass false to seed a manager-suspended
    /// ("86'd") product for V1-RMD-128 regression coverage.
    /// </summary>
    public async Task<Guid> SeedProductAsync(decimal price = 120m, bool isAvailable = true)
    {
        var productId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, is_available, current_price)
            VALUES (@product_id, @sku, 'Lahmacun', 1, 1, @is_available, @price);
            """,
            ("product_id", productId),
            ("sku", "QR-" + productId.ToString("N")[..8]),
            ("is_available", isAvailable),
            ("price", price));

        return productId;
    }
}
