using System.Text.Json;
using ALKAROS.TestHelpers;
using Npgsql;

namespace ALKAROS.Invoicing.Generation.Tests.Fixtures;

/// <summary>
/// Every migration in database/MigrationComposition/order.json, so the invoice reads the real ledger, payments and
/// billing tables. Bills, their payments and adjustments are seeded with foreign-key triggers off
/// (<c>session_replication_role = replica</c>, local to the seeding transaction): the test controls exactly the
/// amounts and KDV rates the generation reads, without building orders and products; CHECK constraints still apply.
/// </summary>
public sealed class InvoiceGenerationTestDatabase : PgTestDatabase
{
    public InvoiceGenerationTestDatabase()
        : base("alkaros_inv002_")
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
            var id = migration.GetProperty("id").GetString()!;
            var file = Directory.GetFiles(migrationRoot, $"{id}-*.up.sql", SearchOption.AllDirectories).Single();
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
        }
    }

    /// <summary>A bill with one item per (rate, gross) pair; net and tax follow the tax-inclusive gross.</summary>
    public async Task<Guid> SeedBillAsync(params (decimal TaxRate, decimal Gross)[] items)
    {
        var billId = Guid.NewGuid();
        await SeedAsync(async (connection, transaction) =>
        {
            await using (var bill = new NpgsqlCommand(
                """
                INSERT INTO billing.bills (bill_id, bill_number, status, payable_amount, currency_code, opened_at, created_at, updated_at)
                VALUES (@bill_id, @bill_number, 'Paid', @payable, 'TRY', now(), now(), now());
                """, connection, transaction))
            {
                bill.Parameters.AddWithValue("bill_id", billId);
                bill.Parameters.AddWithValue("bill_number", "INV002-" + billId.ToString("N")[..10]);
                bill.Parameters.AddWithValue("payable", items.Sum(item => item.Gross));
                await bill.ExecuteNonQueryAsync();
            }

            foreach (var (rate, gross) in items)
            {
                var tax = decimal.Round(gross * rate / (100m + rate), 2, MidpointRounding.AwayFromZero);
                await using var item = new NpgsqlCommand(
                    """
                    INSERT INTO billing.bill_items
                        (bill_item_id, bill_id, order_item_id, product_id, product_name_snapshot, quantity, unit_price,
                         tax_rate, tax_amount, net_amount, gross_amount, line_type, created_at, updated_at)
                    VALUES (@id, @bill_id, @order_item_id, @product_id, 'Test', 1, @net, @rate, @tax, @net, @gross, 'Sale', now(), now());
                    """, connection, transaction);
                item.Parameters.AddWithValue("id", Guid.NewGuid());
                item.Parameters.AddWithValue("bill_id", billId);
                item.Parameters.AddWithValue("order_item_id", Guid.NewGuid());
                item.Parameters.AddWithValue("product_id", Guid.NewGuid());
                item.Parameters.AddWithValue("rate", rate);
                item.Parameters.AddWithValue("tax", tax);
                item.Parameters.AddWithValue("net", gross - tax);
                item.Parameters.AddWithValue("gross", gross);
                await item.ExecuteNonQueryAsync();
            }
        });
        return billId;
    }

    /// <summary>A bill-level adjustment; a deduction (discount) lowers the rate's weight, a fee raises it.</summary>
    public Task SeedAdjustmentAsync(Guid billId, string type, decimal taxRate, decimal gross, bool isDeduction)
        => SeedAsync(async (connection, transaction) =>
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO billing.bill_adjustments
                    (bill_adjustment_id, bill_id, adjustment_type, calculation_type, amount, tax_rate, tax_amount,
                     net_amount, gross_amount, is_deduction, reason, authorized_by, created_at)
                VALUES (@id, @bill_id, @type, 'FixedAmount', @gross, @rate, 0, @gross, @gross, @deduction, 'test',
                        @authorized_by, now());
                """, connection, transaction);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("bill_id", billId);
            command.Parameters.AddWithValue("type", type);
            command.Parameters.AddWithValue("rate", taxRate);
            command.Parameters.AddWithValue("gross", gross);
            command.Parameters.AddWithValue("deduction", isDeduction);
            command.Parameters.AddWithValue("authorized_by", Guid.NewGuid());
            await command.ExecuteNonQueryAsync();
        });

    /// <summary>
    /// What V14-ACC-003 records when a bill is written to an account: an approved payment on the bill and a Charge
    /// ledger row that references it.
    /// </summary>
    public async Task<Guid> SeedChargeAsync(Guid customerId, Guid billId, decimal amount, DateTimeOffset occurredAt)
    {
        var paymentId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        await SeedAsync(async (connection, transaction) =>
        {
            await using var payment = new NpgsqlCommand(
                """
                INSERT INTO payments.payments
                    (payment_id, bill_id, status, currency_code, requested_amount, tendered_amount, approved_amount, initiated_at, tendered_at, approved_at, created_at, updated_at)
                VALUES (@payment_id, @bill_id, 'Approved', 'TRY', @amount, @amount, @amount, now(), now(), now(), now(), now());
                """, connection, transaction);
            payment.Parameters.AddWithValue("payment_id", paymentId);
            payment.Parameters.AddWithValue("bill_id", billId);
            payment.Parameters.AddWithValue("amount", amount);
            await payment.ExecuteNonQueryAsync();
        });
        await RecordAsync(transactionId, customerId, "Charge", amount, "Payment", paymentId, occurredAt);
        return transactionId;
    }

    public async Task<Guid> RecordAsync(
        Guid customerId, string type, decimal amount, DateTimeOffset occurredAt, string referenceType = "Test")
    {
        var id = Guid.NewGuid();
        await RecordAsync(id, customerId, type, amount, referenceType, id, occurredAt);
        return id;
    }

    private Task<int> RecordAsync(
        Guid id, Guid customerId, string type, decimal amount, string referenceType, Guid referenceId, DateTimeOffset occurredAt)
        => ExecuteAsync(
            """
            INSERT INTO customer_account.account_transactions
                (id, customer_id, transaction_type, amount, source_reference_type, source_reference_id, occurred_at)
            VALUES (@id, @customer_id, @type, @amount, @reference_type, @reference_id, @occurred_at);
            """,
            ("id", id),
            ("customer_id", customerId),
            ("type", type),
            ("amount", amount),
            ("reference_type", referenceType),
            ("reference_id", referenceId),
            ("occurred_at", occurredAt));

    public async Task<byte[]> RawBuyerEnvelopeAsync(Guid invoiceId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT buyer_envelope FROM invoicing.invoices WHERE invoice_id = @invoice_id;");
        command.Parameters.AddWithValue("invoice_id", invoiceId);
        return (byte[])(await command.ExecuteScalarAsync())!;
    }

    public async Task<decimal> LedgerSumAsync(Guid customerId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT COALESCE(SUM(amount), 0) FROM customer_account.account_transactions WHERE customer_id = @customer_id;");
        command.Parameters.AddWithValue("customer_id", customerId);
        return (decimal)(await command.ExecuteScalarAsync())!;
    }

    private async Task SeedAsync(Func<NpgsqlConnection, NpgsqlTransaction, Task> seed)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var replica = new NpgsqlCommand("SET LOCAL session_replication_role = replica;", connection, transaction))
            await replica.ExecuteNonQueryAsync();
        await seed(connection, transaction);
        await transaction.CommitAsync();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root (ALKAROS.slnx) not found.");
    }
}
