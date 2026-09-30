using System.Text.Json;
using ALKAROS.TestHelpers;
using Npgsql;

namespace ALKAROS.Privacy.RetentionExecution.Tests.Fixtures;

/// <summary>
/// Every migration in database/MigrationComposition/order.json, so retention reads the real identity, orders, customer,
/// invoicing and purchasing tables. Records are seeded with foreign-key triggers off
/// (<c>session_replication_role = replica</c>, local to the seeding transaction): the test controls exactly the
/// timestamps and flags retention judges; CHECK constraints still apply.
/// </summary>
public sealed class RetentionTestDatabase : PgTestDatabase
{
    public RetentionTestDatabase()
        : base("alkaros_kvk001_")
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

    public async Task<Guid> SeedUserAsync(bool active, DateTimeOffset updatedAt, string displayName = "Ayse Yilmaz")
    {
        var id = Guid.NewGuid();
        await SeedAsync(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, email, phone, active, created_at, updated_at)
            VALUES (@id, @username, 'hash', @name, 'a@b.com', '05001112233', @active, @updated - interval '900 days', @updated);
            """,
            ("id", id), ("username", $"user-{id:N}"[..24]), ("name", displayName), ("active", active), ("updated", updatedAt));
        return id;
    }

    public async Task<Guid> SeedOrderAsync(string status, DateTimeOffset createdAt, string? orderNote, string? itemNote = null)
    {
        var orderId = Guid.NewGuid();
        await SeedAsync(
            """
            INSERT INTO orders.orders
              (order_id, source, table_id, status, confirmation_status, order_number, currency_code, notes, created_at, updated_at, row_version)
            VALUES (@id, 'Cashier', NULL, @status, 'NotRequired', @number, 'TRY', @note, @created, @created, 1);
            INSERT INTO orders.order_items
              (order_item_id, order_id, product_id, product_name_snapshot, quantity, unit_price, tax_rate, tax_amount, net_amount, gross_amount,
               status, kitchen_state, portion_reservation_status, notes, created_at, updated_at, row_version)
            VALUES (@item_id, @id, @product, 'Test', 1, 10, 10, 1, 10, 11, 'Active', 'Served', 'NotApplicable', @item_note, @created, @created, 1);
            """,
            ("id", orderId), ("status", status), ("number", $"K-{orderId:N}"[..20]), ("note", (object?)orderNote ?? DBNull.Value),
            ("created", createdAt), ("item_id", Guid.NewGuid()), ("product", Guid.NewGuid()), ("item_note", (object?)itemNote ?? DBNull.Value));
        return orderId;
    }

    public async Task<Guid> SeedReservationAsync(string status, DateTimeOffset reservedAt, string reason = "Mehmet Aile")
    {
        var id = Guid.NewGuid();
        await SeedAsync(
            """
            INSERT INTO table_mgmt.table_reservations
              (table_reservation_id, table_id, actor_type, status, reason, party_size, reserved_at, row_version)
            VALUES (@id, @table, 'User', @status, @reason, 4, @reserved, 1);
            """,
            ("id", id), ("table", Guid.NewGuid()), ("status", status), ("reason", reason), ("reserved", reservedAt));
        return id;
    }

    public async Task<Guid> SeedCustomerAsync(DateTimeOffset createdAt, DateTimeOffset? lastTransaction = null, decimal balance = 0m, DateTimeOffset? invoicedAt = null)
    {
        var id = Guid.NewGuid();
        await SeedAsync(
            "INSERT INTO customer_data.profiles (customer_id, envelope_bytes, created_at) VALUES (@id, '\\x00'::bytea, @created);",
            ("id", id), ("created", createdAt));
        if (lastTransaction is { } occurred)
        {
            await SeedAsync(
                """
                INSERT INTO customer_account.account_transactions
                    (id, customer_id, transaction_type, amount, source_reference_type, source_reference_id, occurred_at)
                VALUES (@tx, @id, 'Charge', 10, 'Test', @tx, @occurred);
                INSERT INTO customer_account.balances (customer_id, current_balance, last_transaction_at, updated_at)
                VALUES (@id, @balance, @occurred, @occurred);
                """,
                ("tx", Guid.NewGuid()), ("id", id), ("occurred", occurred), ("balance", balance));
        }

        if (invoicedAt is { } invoiced)
        {
            await SeedAsync(
                """
                INSERT INTO invoicing.invoices
                    (invoice_id, source_set_id, period_id, customer_id, profile, ubl_profile_id, invoice_type_code, currency_code,
                     issue_date, status, buyer_envelope, line_extension_amount, tax_total, payable_amount, created_at, created_by)
                VALUES (@invoice, @set, @period, @id, 'EFatura', 'TEMELFATURA', 'SATIS', 'TRY', (@invoiced AT TIME ZONE 'UTC')::date,
                        'Draft', '\x00'::bytea, 100, 0, 100, @invoiced, @by);
                """,
                ("invoice", Guid.NewGuid()), ("set", Guid.NewGuid()), ("period", Guid.NewGuid()), ("id", id), ("invoiced", invoiced), ("by", Guid.NewGuid()));
        }

        return id;
    }

    public Task RequestAnonymizationAsync(Guid customerId, string status = "Pending")
        => SeedAsync(
            """
            INSERT INTO customer_data.anonymization_requests (id, customer_id, status, requested_at, blocked_reason)
            VALUES (@id, @customer, @status, now(), CASE WHEN @status = 'RetentionBlocked' THEN 'open balance' END);
            """,
            ("id", Guid.NewGuid()), ("customer", customerId), ("status", status));

    public async Task<Guid> SeedSupplierAsync(DateTimeOffset createdAt, DateTimeOffset? lastOrder = null)
    {
        var id = Guid.NewGuid();
        await SeedAsync(
            "INSERT INTO purchasing.suppliers (supplier_id, code, name, created_at, updated_at) VALUES (@id, @code, 'Tedarikci', @created, @created);",
            ("id", id), ("code", $"S-{id:N}"[..16]), ("created", createdAt));
        if (lastOrder is { } ordered)
        {
            await SeedAsync(
                """
                INSERT INTO purchasing.purchase_orders (order_id, order_number, supplier_id, status, destination_location_id, created_at, updated_at)
                VALUES (@order, @number, @id, 'Received', @location, @ordered, @ordered);
                """,
                ("order", Guid.NewGuid()), ("number", $"PO-{Guid.NewGuid():N}"[..20]), ("id", id), ("location", Guid.NewGuid()), ("ordered", ordered));
        }

        return id;
    }

    private async Task SeedAsync(string sql, params (string Name, object Value)[] parameters)
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
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ALKAROS.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root (ALKAROS.slnx) not found.");
    }
}
