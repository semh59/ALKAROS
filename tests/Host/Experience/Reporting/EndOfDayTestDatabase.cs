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
            INSERT INTO billing.bills (bill_id, bill_number, status, payable_amount, opened_at, created_at, updated_at)
            VALUES (@bill, @number, 'Open', @amount, @at, @at, @at);
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
        command.Parameters.AddWithValue("declinedAt", approved ? DBNull.Value : at);
        await command.ExecuteNonQueryAsync();
    }

    public async Task SeedOrderAsync(DateTimeOffset submittedAt, string status, string source = "Cashier")
    {
        var orderId = Guid.NewGuid();
        await using var command = DataSource.CreateCommand(
            """
            INSERT INTO orders.orders (order_id, source, status, confirmation_status, order_number, submitted_at, created_at, updated_at)
            VALUES (@order, @source, @status, 'NotRequired', @number, @at, @at, @at);
            """);
        command.Parameters.AddWithValue("order", orderId);
        command.Parameters.AddWithValue("source", source);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("number", "EOD-" + orderId.ToString("N")[..12]);
        command.Parameters.AddWithValue("at", submittedAt);
        await command.ExecuteNonQueryAsync();
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
