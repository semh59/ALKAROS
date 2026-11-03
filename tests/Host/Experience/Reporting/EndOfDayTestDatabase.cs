using System.Text.Json;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.Reporting.Tests;

/// <summary>
/// Applies the full runtime migration manifest — EOD wiring pulls in the
/// identity permission catalog (reports.view, reports.close-day migration
/// 133). Three tokens: a manager (holds both reports.view AND
/// reports.close-day), a supervisor (holds reports.view only — can read but
/// not open/close a day), and a denied user (holds neither).
/// </summary>
public sealed class EndOfDayTestDatabase : PgTestDatabase
{
    public static readonly Guid ManagerUserId = Guid.NewGuid();
    public static readonly Guid SupervisorUserId = Guid.NewGuid();
    public static readonly Guid DeniedUserId = Guid.NewGuid();
    public const string ManagerToken = "eod-manager-test-token";
    public const string SupervisorToken = "eod-supervisor-test-token";
    public const string DeniedToken = "eod-denied-test-token";

    public EndOfDayTestDatabase() : base("alkaros_rmd249_")
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

        await RunAsync(
            DataSource,
            $$"""
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES
                ('{{ManagerUserId:D}}', 'eod-manager-{{ManagerUserId:N}}', 'unused', 'EOD Manager', TRUE),
                ('{{SupervisorUserId:D}}', 'eod-supervisor-{{SupervisorUserId:N}}', 'unused', 'EOD Supervisor', TRUE),
                ('{{DeniedUserId:D}}', 'eod-denied-{{DeniedUserId:N}}', 'unused', 'EOD Denied', TRUE);

            INSERT INTO identity.roles (role_id, code, name)
            VALUES
                ('{{Guid.NewGuid():D}}', 'eod-manager-role-{{ManagerUserId:N}}', 'EOD Manager Test Role'),
                ('{{Guid.NewGuid():D}}', 'eod-supervisor-role-{{SupervisorUserId:N}}', 'EOD Supervisor Test Role');

            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            SELECT gen_random_uuid(), r.role_id, p.permission_id
            FROM identity.roles r, identity.permissions p
            WHERE r.code = 'eod-manager-role-{{ManagerUserId:N}}' AND p.code IN ('reports.view', 'reports.close-day');

            INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
            SELECT gen_random_uuid(), r.role_id, p.permission_id
            FROM identity.roles r, identity.permissions p
            WHERE r.code = 'eod-supervisor-role-{{SupervisorUserId:N}}' AND p.code = 'reports.view';

            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            SELECT gen_random_uuid(), '{{ManagerUserId:D}}', role_id
            FROM identity.roles WHERE code = 'eod-manager-role-{{ManagerUserId:N}}';

            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            SELECT gen_random_uuid(), '{{SupervisorUserId:D}}', role_id
            FROM identity.roles WHERE code = 'eod-supervisor-role-{{SupervisorUserId:N}}';

            INSERT INTO identity.device_sessions
                (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES
                ('{{Guid.NewGuid():D}}', '{{ManagerUserId:D}}', 'manager:test',
                 '{{DeviceSessionToken.Hash(ManagerToken)}}', now(), now() + interval '1 hour'),
                ('{{Guid.NewGuid():D}}', '{{SupervisorUserId:D}}', 'supervisor:test',
                 '{{DeviceSessionToken.Hash(SupervisorToken)}}', now(), now() + interval '1 hour'),
                ('{{Guid.NewGuid():D}}', '{{DeniedUserId:D}}', 'manager:test-denied',
                 '{{DeviceSessionToken.Hash(DeniedToken)}}', now(), now() + interval '1 hour');
            """);
    }

    /// <summary>
    /// V1-RMD-421: seeds one approved payment and one submitted order at each given instant. Payments get a bill of
    /// their own; <paramref name="paymentStatus"/> Declined leaves the amount unapproved.
    /// </summary>
    public async Task SeedPaymentAsync(DateTimeOffset at, decimal amount, string paymentStatus = "Approved")
    {
        var billId = Guid.NewGuid();
        var approved = paymentStatus == "Approved";
        await using var command = DataSource.CreateCommand(
            """
            INSERT INTO billing.bills (bill_id, bill_number, status, payable_amount, opened_at, closed_at, created_at, updated_at)
            VALUES (@bill, @number, 'Paid', @amount, @at, @at, @at, @at);
            INSERT INTO payments.payments (payment_id, bill_id, status, requested_amount, tendered_amount, approved_amount,
                                           initiated_at, tendered_at, approved_at, declined_at, created_at, updated_at)
            VALUES (gen_random_uuid(), @bill, @status, @amount, @amount, @approvedAmount,
                    @at, @at, @approvedAt, @declinedAt, @at, @at);
            """);
        command.Parameters.AddWithValue("bill", billId);
        command.Parameters.AddWithValue("number", "EOD-" + billId.ToString("N")[..12]);
        command.Parameters.AddWithValue("amount", amount);
        command.Parameters.AddWithValue("at", at);
        command.Parameters.AddWithValue("status", paymentStatus);
        command.Parameters.AddWithValue("approvedAmount", approved ? amount : DBNull.Value);
        command.Parameters.AddWithValue("approvedAt", approved ? at : DBNull.Value);
        command.Parameters.AddWithValue("declinedAt", paymentStatus == "Declined" ? at : DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> SeedOrderAsync(
        DateTimeOffset submittedAt, string status, string source = "Cashier", Guid? servingUserId = null, Guid? tableId = null,
        decimal total = 0m, decimal discount = 0m)
    {
        var orderId = Guid.NewGuid();
        await ExecuteWithoutForeignKeysAsync(
            """
            INSERT INTO orders.orders (order_id, source, status, confirmation_status, order_number, submitted_at, created_at, updated_at,
                                       serving_user_id, table_id, total, discount_total)
            VALUES (@order, @source, @status, 'NotRequired', @number, @at, @at, @at, @serving, @table, @total, @discount);
            """,
            ("order", orderId), ("source", source), ("status", status), ("number", "EOD-" + orderId.ToString("N")[..12]),
            ("at", submittedAt), ("serving", (object?)servingUserId ?? DBNull.Value), ("table", (object?)tableId ?? DBNull.Value),
            ("total", total), ("discount", discount));
        return orderId;
    }

    public async Task<Guid> SeedBillAsync(string status, DateTimeOffset? closedAt = null)
    {
        var billId = Guid.NewGuid();
        await ExecuteWithoutForeignKeysAsync(
            """
            INSERT INTO billing.bills (bill_id, bill_number, status, payable_amount, opened_at, closed_at, created_at, updated_at)
            VALUES (@bill, @number, @status, 100, now(), @closed, now(), now());
            """,
            ("bill", billId), ("number", "EOD-" + billId.ToString("N")[..12]), ("status", status),
            ("closed", (object?)closedAt ?? DBNull.Value));
        return billId;
    }

    public Task SeedTipAsync(Guid billId, decimal amount)
        => ExecuteWithoutForeignKeysAsync(
            """
            INSERT INTO billing.bill_adjustments (bill_adjustment_id, bill_id, adjustment_type, calculation_type, amount, net_amount,
                                                  gross_amount, is_deduction, reason, authorized_by, created_at)
            VALUES (gen_random_uuid(), @bill, 'Tip', 'FixedAmount', @amount, @amount, @amount, FALSE, 'tip', gen_random_uuid(), now());
            """,
            ("bill", billId), ("amount", amount));

    public Task SeedAuditAsync(string eventName, Guid aggregateId, DateTimeOffset at)
        => ExecuteWithoutForeignKeysAsync(
            """
            INSERT INTO audit.audit_events (id, event_name, aggregate_type, aggregate_id, actor_type, correlation_id, occurred_at)
            VALUES (gen_random_uuid(), @name, 'Order', @aggregate, 'User', 'eod-test', @at);
            """,
            ("name", eventName), ("aggregate", aggregateId), ("at", at));

    public async Task SeedPrintJobAsync(string station, DateTimeOffset createdAt, DateTimeOffset? failedAt, string status)
    {
        var printerId = Guid.NewGuid();
        await ExecuteWithoutForeignKeysAsync(
            """
            INSERT INTO kitchen.printers (id, name, station_id) VALUES (@printer, @name, @station);
            INSERT INTO kitchen.print_jobs (id, ticket_id, printer_id, idempotency_key, payload, status, created_at, failed_at)
            VALUES (gen_random_uuid(), gen_random_uuid(), @printer, @key, 'x', @status, @created, @failed);
            """,
            ("printer", printerId), ("name", "P-" + printerId.ToString("N")), ("station", station), ("key", printerId.ToString("N")),
            ("status", status), ("created", createdAt), ("failed", (object?)failedAt ?? DBNull.Value));
    }

    public Task SeedCashSessionAsync(
        string status, DateTimeOffset? closedAt, decimal expected, decimal actual, bool supervisorOverride = false, bool selfReconciled = false)
        => ExecuteWithoutForeignKeysAsync(
            """
            INSERT INTO cash.cash_sessions (cash_session_id, cashier_user_id, terminal_id, status, opening_balance, expected_cash, actual_cash,
                difference, opened_at, closed_at, is_supervisor_override, override_reason, reconciled_self_approved, difference_explanation, created_at, updated_at)
            VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), @status, 0, @expected, @actual, @actual - @expected,
                now(), @closed, @override, CASE WHEN @override THEN 'x' END, @self, CASE WHEN @actual <> @expected THEN 'x' END, now(), now());
            """,
            ("status", status), ("expected", expected), ("actual", actual), ("closed", (object?)closedAt ?? DBNull.Value),
            ("override", supervisorOverride), ("self", selfReconciled));

    public async Task<long> CountAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = DataSource.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Seeds rows that reference tables and users the test does not need; the foreign keys are not under test here.</summary>
    private async Task ExecuteWithoutForeignKeysAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var relax = new Npgsql.NpgsqlCommand("SET LOCAL session_replication_role = replica;", connection, transaction))
            await relax.ExecuteNonQueryAsync();
        await using var command = new Npgsql.NpgsqlCommand(sql, connection, transaction);
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
