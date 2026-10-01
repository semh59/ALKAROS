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
