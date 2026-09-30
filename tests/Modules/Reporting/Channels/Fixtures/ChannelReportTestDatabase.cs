using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.TestHelpers;

namespace ALKAROS.Reporting.Channels.Tests.Fixtures;

/// <summary>
/// The full runtime migration manifest (the report reads orders, the online ordering inbox and the
/// reconciliation cases) plus seed helpers that write rows with explicit timestamps, so a golden dataset
/// can sit exactly on business-date boundaries.
/// </summary>
public sealed class ChannelReportTestDatabase : PgTestDatabase
{
    public ChannelReportTestDatabase() : base("alkaros_rpt_chn_")
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

    /// <summary>An order whose stored amounts follow the Order aggregate: prices are tax-inclusive, so total = subtotal - discount and the 20% tax is contained in it.</summary>
    public async Task SeedOrderAsync(
        string source, string status, decimal total, DateTimeOffset createdAt, string? externalOrderId = null, decimal discount = 0m,
        string provider = "yemeksepeti")
    {
        var orderId = Guid.NewGuid();
        await using var command = DataSource.CreateCommand(
            """
            INSERT INTO orders.orders
                (order_id, source, source_external_id, status, confirmation_status, order_number,
                 subtotal, discount_total, tax_total, total, created_at, updated_at)
            VALUES ($1, $2, $3, $4, 'NotRequired', $5, $6 + $9, $9, $8, $6, $7, $7);
            """);
        command.Parameters.AddWithValue(orderId);
        command.Parameters.AddWithValue(source);
        command.Parameters.AddWithValue((object?)externalOrderId ?? DBNull.Value).NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Text;
        command.Parameters.AddWithValue(status);
        command.Parameters.AddWithValue("T-" + orderId.ToString("N")[..16]);
        command.Parameters.AddWithValue(total);
        command.Parameters.AddWithValue(createdAt.UtcDateTime);
        command.Parameters.AddWithValue(Math.Round(total * 20m / 120m, 2, MidpointRounding.AwayFromZero));
        command.Parameters.AddWithValue(discount);
        await command.ExecuteNonQueryAsync();

        // V12-REC-002: an online order with a platform number is linked to its platform (V12-ONL-006).
        if (source == "Online" && externalOrderId is not null)
        {
            await using var link = DataSource.CreateCommand(
                "INSERT INTO online_ordering.online_orders (order_id, provider, external_order_id) VALUES ($1, $2, $3);");
            link.Parameters.AddWithValue(orderId);
            link.Parameters.AddWithValue(provider);
            link.Parameters.AddWithValue(externalOrderId);
            await link.ExecuteNonQueryAsync();
        }
    }

    public async Task SeedInboxAsync(string externalOrderId, string outcome, DateTimeOffset receivedAt, string provider = "yemeksepeti")
    {
        var inboxId = Guid.NewGuid();
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inboxId.ToString()))).ToLowerInvariant();
        await using var command = DataSource.CreateCommand(
            """
            INSERT INTO online_ordering.provider_inbox
                (provider, inbox_id, event_key, external_order_id, provider_status, body_sha256, payload_envelope,
                 received_at, processed_at, processing_outcome, outcome_detail)
            VALUES ($6, $1, $2, $3, 'RECEIVED', $2, '\x00'::bytea, $4, $4, $5, '{}'::jsonb);
            """);
        command.Parameters.AddWithValue(inboxId);
        command.Parameters.AddWithValue(key);
        command.Parameters.AddWithValue(externalOrderId);
        command.Parameters.AddWithValue(receivedAt.UtcDateTime);
        command.Parameters.AddWithValue(outcome);
        command.Parameters.AddWithValue(provider);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>V12-RMD-006: runs a statement against the fixture database (to simulate a future order status).</summary>
    public async Task ExecAsync(string sql)
    {
        await using var command = DataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> SeedCaseAsync(string kind, string status, decimal amount, DateTimeOffset openedAt)
    {
        var caseId = Guid.NewGuid();
        await using var command = DataSource.CreateCommand(
            """
            INSERT INTO reconciliation.cases
                (case_id, deduplication_key, case_type, source_a_ref, source_b_ref, discrepancy_amount, severity, status, opened_at, details)
            VALUES ($1, $2, 'OnlineOrderMismatch', 'a', 'b', $3, 'High', $4, $5, $6::jsonb);
            """);
        command.Parameters.AddWithValue(caseId);
        command.Parameters.AddWithValue("test:" + caseId);
        command.Parameters.AddWithValue(amount);
        command.Parameters.AddWithValue(status);
        command.Parameters.AddWithValue(openedAt.UtcDateTime);
        command.Parameters.AddWithValue(JsonSerializer.Serialize(new { kind, nextAction = "x" }));
        await command.ExecuteNonQueryAsync();
        return caseId;
    }

    public async Task SeedRetryAttemptAsync(Guid caseId, DateTimeOffset performedAt)
    {
        await using var command = DataSource.CreateCommand(
            """
            INSERT INTO reconciliation.online_order_retry_attempts (attempt_id, case_id, action, outcome, source_ref, performed_by, performed_at)
            VALUES (gen_random_uuid(), $1, 'ResendProviderUpdate', 'Requeued', 'outbox_messages:x', gen_random_uuid(), $2);
            """);
        command.Parameters.AddWithValue(caseId);
        command.Parameters.AddWithValue(performedAt.UtcDateTime);
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
