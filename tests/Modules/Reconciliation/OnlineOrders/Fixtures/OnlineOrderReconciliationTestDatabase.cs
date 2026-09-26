using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.TestHelpers;
using Npgsql;

namespace ALKAROS.Reconciliation.OnlineOrders.Tests.Fixtures;

/// <summary>
/// The full runtime migration manifest (every source this task reads lives in a different module's
/// schema: online_ordering, orders, the shared outbox) plus seed helpers that write those sources the way
/// V12-ONL-002/003/005 write them.
/// </summary>
public sealed class OnlineOrderReconciliationTestDatabase : PgTestDatabase
{
    public const string StatusUpdateEventType = "online-ordering.yemeksepeti.status-update-requested.v1";

    public OnlineOrderReconciliationTestDatabase() : base("alkaros_rec_onl_")
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

    public static string MigrationScript(string suffix)
    {
        var files = Directory.GetFiles(
            Path.Combine(FindRepositoryRoot(), "database", "migrations", "V12", "V12-REC-001"), $"149-*.{suffix}.sql");
        return File.ReadAllText(files.Single());
    }

    public async Task<Guid> SeedInboxAsync(
        string externalOrderId, string outcome, Guid? orderId = null, object? detail = null, int attempts = 0,
        string provider = "yemeksepeti")
    {
        var inboxId = Guid.NewGuid();
        var eventKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inboxId.ToString()))).ToLowerInvariant();
        await using var command = DataSource.CreateCommand(
            """
            INSERT INTO online_ordering.provider_inbox
                (provider, inbox_id, event_key, external_order_id, provider_status, body_sha256, payload_envelope,
                 processed_at, processing_outcome, order_id, outcome_detail, processing_attempts)
            VALUES ($8, $1, $2, $3, 'RECEIVED', $2, '\x00'::bytea, now(), $4, $5, $6::jsonb, $7);
            """);
        command.Parameters.AddWithValue(inboxId);
        command.Parameters.AddWithValue(eventKey);
        command.Parameters.AddWithValue(externalOrderId);
        command.Parameters.AddWithValue(outcome);
        command.Parameters.AddWithValue(orderId is { } id ? id : DBNull.Value);
        command.Parameters.AddWithValue(detail is null ? DBNull.Value : JsonSerializer.Serialize(detail));
        command.Parameters.AddWithValue(attempts);
        command.Parameters.AddWithValue(provider);
        await command.ExecuteNonQueryAsync();
        return inboxId;
    }

    public async Task<Guid> SeedOnlineOrderAsync(string externalOrderId, string status, decimal total, string provider = "yemeksepeti")
    {
        var orderId = Guid.NewGuid();
        // V12-REC-002: an online order is linked to its platform (V12-ONL-006), as intake does.
        await using var command = DataSource.CreateCommand(
            """
            WITH created AS (
                INSERT INTO orders.orders
                    (order_id, source, source_external_id, status, confirmation_status, order_number, total, created_at, updated_at)
                VALUES ($1, 'Online', $2, $3, 'Accepted', $4, $5, now(), now())
                RETURNING order_id)
            INSERT INTO online_ordering.online_orders (order_id, provider, external_order_id)
            SELECT order_id, $6, $2 FROM created;
            """);
        command.Parameters.AddWithValue(orderId);
        command.Parameters.AddWithValue(externalOrderId);
        command.Parameters.AddWithValue(status);
        command.Parameters.AddWithValue("YS-" + orderId.ToString("N")[..12]);
        command.Parameters.AddWithValue(total);
        command.Parameters.AddWithValue(provider);
        await command.ExecuteNonQueryAsync();
        return orderId;
    }

    /// <summary>A status update in the outbox, serialized the way V12-ONL-003 writes it.</summary>
    public async Task<Guid> SeedStatusUpdateAsync(string externalOrderId, string status, int attempts = 5, string? eventType = null)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            new { requestId = Guid.NewGuid(), externalOrderId, status = 2, reason = 0, items = new[] { new { sku = "sku-1", quantity = 1 } } });
        await using var command = DataSource.CreateCommand(
            """
            INSERT INTO outbox_messages (event_type, aggregate_type, aggregate_id, payload_envelope, status, attempt_count, last_error)
            VALUES ($1, 'yemeksepeti_order', $2, $3, $4, $5, CASE WHEN $4 = 'dead' THEN 'provider unreachable' END)
            RETURNING id;
            """);
        // V12-TGO-003: another platform's status update queue (its payload also carries externalOrderId).
        command.Parameters.AddWithValue(eventType ?? StatusUpdateEventType);
        command.Parameters.AddWithValue(Guid.NewGuid());
        command.Parameters.AddWithValue(payload);
        command.Parameters.AddWithValue(status);
        command.Parameters.AddWithValue(attempts);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    public async Task SetOutboxStatusAsync(Guid messageId, string status)
    {
        await using var command = DataSource.CreateCommand("UPDATE outbox_messages SET status = $2 WHERE id = $1;");
        command.Parameters.AddWithValue(messageId);
        command.Parameters.AddWithValue(status);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<(string Status, int Attempts)> OutboxStateAsync(Guid messageId)
    {
        await using var command = DataSource.CreateCommand("SELECT status, attempt_count FROM outbox_messages WHERE id = $1;");
        command.Parameters.AddWithValue(messageId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetString(0), reader.GetInt32(1));
    }

    public async Task<(string? Outcome, int Attempts)> InboxStateAsync(Guid inboxId)
    {
        await using var command = DataSource.CreateCommand(
            "SELECT processing_outcome, processing_attempts FROM online_ordering.provider_inbox WHERE inbox_id = $1;");
        command.Parameters.AddWithValue(inboxId);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.IsDBNull(0) ? null : reader.GetString(0), reader.GetInt32(1));
    }

    public async Task SeedAvailabilityStateAsync(
        string channel, Guid productId, int desired, int? delivered, int attempts, int desiredMinutesAgo)
    {
        await using var command = DataSource.CreateCommand(
            """
            INSERT INTO online_ordering.availability_states
                (channel, product_id, external_sku, desired_quantity, desired_version, desired_at, delivered_quantity, delivery_attempts)
            VALUES ($1, $2, $3, $4, 1, now() - make_interval(mins => $5), $6, $7);
            """);
        command.Parameters.AddWithValue(channel);
        command.Parameters.AddWithValue(productId);
        command.Parameters.AddWithValue("sku-" + productId.ToString("N")[..8]);
        command.Parameters.AddWithValue(desired);
        command.Parameters.AddWithValue(desiredMinutesAgo);
        command.Parameters.AddWithValue(delivered is { } value ? value : DBNull.Value);
        command.Parameters.AddWithValue(attempts);
        await command.ExecuteNonQueryAsync();
    }

    public async Task MarkAvailabilityDeliveredAsync(string channel, Guid productId)
    {
        await using var command = DataSource.CreateCommand(
            """
            UPDATE online_ordering.availability_states
            SET delivered_quantity = desired_quantity, delivered_version = desired_version, delivery_attempts = 0
            WHERE channel = $1 AND product_id = $2;
            """);
        command.Parameters.AddWithValue(channel);
        command.Parameters.AddWithValue(productId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<long> CountAsync(string sql, params object[] parameters)
    {
        await using var command = DataSource.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
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
