using System.Text.Json;
using ALKAROS.TestHelpers;
using Npgsql;

namespace ALKAROS.Reporting.ProductMargin.Tests.Fixtures;

/// <summary>
/// The full runtime migration manifest plus seed helpers for the ledgers the report reads. Rows are written with foreign-key
/// triggers off (<c>session_replication_role = replica</c>, local to each seeding transaction) and explicit timestamps, so a
/// golden dataset can sit exactly on service-day boundaries.
/// </summary>
public sealed class ProductMarginTestDatabase : PgTestDatabase
{
    public ProductMarginTestDatabase() : base("alkaros_rpt_mgn_")
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

    public async Task<Guid> SeedProductAsync(string name)
    {
        var id = Guid.NewGuid();
        await WriteAsync(
            "INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price) VALUES (@id, @sku, @name, 1, 1, 100);",
            ("id", id), ("sku", "mgn-" + id.ToString("N")[..10]), ("name", name));
        return id;
    }

    public async Task<Guid> SeedStockItemAsync(string name)
    {
        var id = Guid.NewGuid();
        await WriteAsync(
            "INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code) VALUES (@id, @code, @name, 'RawMaterial', 'kg');",
            ("id", id), ("code", "MGN-" + id.ToString("N")[..10]), ("name", name));
        return id;
    }

    public async Task SeedReceiptAsync(Guid stockItemId, decimal acceptedQuantity, decimal unitPrice, DateTimeOffset receivedAt)
    {
        var receiptId = Guid.NewGuid();
        await WriteAsync(
            """
            INSERT INTO purchasing.goods_receipts (receipt_id, receipt_number, order_id, supplier_id, destination_location_id, received_at, received_by)
            VALUES (@receipt, @number, @order, @supplier, @location, @received_at, 'test');
            INSERT INTO purchasing.goods_receipt_items
                (item_id, receipt_id, order_line_id, stock_item_id, delivered_quantity, accepted_quantity, unit_code, unit_price)
            VALUES (@item, @receipt, @line, @stock_item, @qty, @qty, 'kg', @price);
            """,
            ("receipt", receiptId), ("number", "R-" + receiptId.ToString("N")[..12]), ("order", Guid.NewGuid()),
            ("supplier", Guid.NewGuid()), ("location", Guid.NewGuid()), ("received_at", receivedAt.UtcDateTime),
            ("item", Guid.NewGuid()), ("line", Guid.NewGuid()), ("stock_item", stockItemId), ("qty", acceptedQuantity), ("price", unitPrice));
    }

    /// <summary>One bill with one line; returns the order item id the stock movements refer to.</summary>
    public async Task<Guid> SeedBillLineAsync(
        Guid productId, string productName, decimal quantity, decimal netAmount, DateTimeOffset openedAt,
        string lineType = "Sale", string billStatus = "Paid", decimal billDiscount = 0m)
    {
        var billId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        await WriteAsync(
            """
            INSERT INTO billing.bills (bill_id, bill_number, status, discount_total, opened_at, created_at, updated_at)
            VALUES (@bill, @number, @status, @discount, @opened, @opened, @opened);
            INSERT INTO billing.bill_items
                (bill_item_id, bill_id, order_item_id, product_id, product_name_snapshot, quantity, unit_price, tax_rate, net_amount, gross_amount, line_type, created_at, updated_at)
            VALUES (@item, @bill, @order_item, @product, @name, @qty, 100, 10, @net, @net, @line_type, @opened, @opened);
            """,
            ("bill", billId), ("number", "B-" + billId.ToString("N")[..12]), ("status", billStatus), ("discount", billDiscount),
            ("opened", openedAt.UtcDateTime), ("item", Guid.NewGuid()), ("order_item", orderItemId), ("product", productId),
            ("name", productName), ("qty", quantity), ("net", netAmount), ("line_type", lineType));
        return orderItemId;
    }

    public async Task<Guid> SeedConsumptionAsync(Guid orderItemId, Guid stockItemId, decimal quantity)
    {
        var id = Guid.NewGuid();
        await WriteAsync(
            """
            INSERT INTO inventory.stock_movements
                (stock_movement_id, stock_item_id, stock_location_id, movement_type, direction, quantity, unit_code, source_type, source_reference_id)
            VALUES (@id, @stock_item, @location, 'Consumption', 'Out', @qty, 'kg', 'Order', @order_item);
            """,
            ("id", id), ("stock_item", stockItemId), ("location", Guid.NewGuid()), ("qty", quantity), ("order_item", orderItemId));
        return id;
    }

    public Task SeedReversalAsync(Guid consumptionId, Guid stockItemId, decimal quantity)
        => WriteAsync(
            """
            INSERT INTO inventory.stock_movements
                (stock_movement_id, stock_item_id, stock_location_id, movement_type, direction, quantity, unit_code, source_type, source_reference_id)
            VALUES (@id, @stock_item, @location, 'Reversal', 'In', @qty, 'kg', 'StockMovement', @consumption);
            """,
            ("id", Guid.NewGuid()), ("stock_item", stockItemId), ("location", Guid.NewGuid()), ("qty", quantity), ("consumption", consumptionId));

    private async Task WriteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var replica = new NpgsqlCommand("SET LOCAL session_replication_role = replica;", connection, transaction))
            await replica.ExecuteNonQueryAsync();
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
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
