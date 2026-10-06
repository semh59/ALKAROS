using System.Text.Json;
using ALKAROS.TestHelpers;
using Npgsql;

namespace ALKAROS.Purchasing.PurchaseInvoices.Tests;

/// <summary>The full runtime migration manifest, so the real purchasing, inventory and supplier tables are in place.</summary>
public sealed class PurchaseInvoiceTestDatabase : PgTestDatabase
{
    public PurchaseInvoiceTestDatabase() : base("alkaros_pur_inv_")
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
    }

    public async Task<Guid> SeedStockItemAsync(string name, string unit = "kg")
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code) VALUES (@id, @code, @name, 'RawMaterial', @unit);",
            ("id", id), ("code", "PI-" + id.ToString("N")[..10]), ("name", name), ("unit", unit));
        return id;
    }

    public async Task<Guid> SeedSupplierAsync(string taxNumber)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO purchasing.suppliers (supplier_id, code, name, tax_number, active) VALUES (@id, @code, 'Tedarikçi', @tax, true);",
            ("id", id), ("code", "S" + id.ToString("N")[..8].ToUpperInvariant()), ("tax", taxNumber));
        return id;
    }

    public async Task<Guid> SeedLocationAsync()
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO inventory.stock_locations (id, code, name, location_type) VALUES (@id, @code, 'Depo', 'Warehouse');",
            ("id", id), ("code", "L-" + id.ToString("N")[..10]));
        return id;
    }

    public async Task<(decimal Quantity, string Unit, decimal UnitPrice, DateOnly ReceivedAtDate)> ReadReceiptItemAsync(Guid receiptId)
    {
        await using var command = DataSource.CreateCommand(
            """
            SELECT i.accepted_quantity, i.unit_code, i.unit_price, (r.received_at AT TIME ZONE 'Europe/Istanbul')::date
            FROM purchasing.goods_receipt_items i JOIN purchasing.goods_receipts r ON r.receipt_id = i.receipt_id
            WHERE i.receipt_id = @id;
            """);
        command.Parameters.AddWithValue("id", receiptId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetDecimal(0), reader.GetString(1), reader.GetDecimal(2), reader.GetFieldValue<DateOnly>(3));
    }

    public async Task<decimal> ReadOnHandAsync(Guid stockItemId, Guid locationId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT on_hand_quantity FROM inventory.stock_balances WHERE stock_item_id = @item AND stock_location_id = @location;");
        command.Parameters.AddWithValue("item", stockItemId);
        command.Parameters.AddWithValue("location", locationId);
        return (decimal)(await command.ExecuteScalarAsync() ?? 0m);
    }

    public async Task<long> CountMovementsAsync(Guid receiptId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT COUNT(*) FROM inventory.stock_movements WHERE source_reference_id = @id AND movement_type = 'PurchaseReceipt';");
        command.Parameters.AddWithValue("id", receiptId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public async Task<long> CountReturnMovementsAsync(Guid invoiceId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT COUNT(*) FROM inventory.stock_movements WHERE source_reference_id = @id AND movement_type = 'Return' AND direction = 'Out';");
        command.Parameters.AddWithValue("id", invoiceId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public async Task<long> CountReceiptsAsync(Guid invoiceId)
    {
        await using var command = DataSource.CreateCommand("SELECT COUNT(*) FROM purchasing.goods_receipts WHERE invoice_id = @id;");
        command.Parameters.AddWithValue("id", invoiceId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public Task SetStatusAsync(Guid invoiceId, string status)
        => ExecuteAsync("UPDATE purchasing.purchase_invoices SET status = @status WHERE invoice_id = @id;", ("id", invoiceId), ("status", status));

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
