using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Npgsql;

namespace ALKAROS.OnlineOrdering.Yemeksepeti.WebhookInbox;

public enum WebhookReceiptOutcome
{
    /// <summary>A new provider event was durably stored.</summary>
    Stored,

    /// <summary>The same provider event was already stored; the delivery is acknowledged again, nothing new is written.</summary>
    Duplicate,

    /// <summary>No webhook secret is configured, so the channel is closed; nothing is read or stored.</summary>
    ChannelNotConfigured,

    /// <summary>The delivery did not present the configured secret; nothing is stored.</summary>
    Unauthenticated,

    /// <summary>The body is larger than any order payload should be; nothing is stored.</summary>
    TooLarge,

    /// <summary>The body is not an order payload with an order id and a status; nothing is stored.</summary>
    Malformed
}

public sealed record WebhookReceipt(WebhookReceiptOutcome Outcome, Guid? InboxId);

/// <summary>
/// V12-ONL-001. Authenticates one Yemeksepeti webhook delivery and stores it exactly once
/// before any asynchronous processing.
///
/// UNVERIFIED DRAFT (provider contract): the authentication and payload rules below follow
/// the public Partner API v2.0.2 document and the POS partner-picking FAQ only — the Partner
/// Portal secret is sent back in the <c>Authorization</c> header, deliveries time out after
/// 10 s and are retried up to 5 times, the order payload carries <c>order_id</c>,
/// <c>status</c> and <c>sys.updated_at</c>. None of it has been checked against a real
/// sandbox delivery (V0-YSP-001 is Blocked; V12-GOV-004 waiver).
/// </summary>
public sealed class YemeksepetiWebhookInbox
{
    /// <summary>Configured as <c>ALKAROS_SECRET_YEMEKSEPETI_WEBHOOK_SECRET</c>; absent means the channel is off.</summary>
    public static readonly SecretReference WebhookSecret = new("yemeksepeti-webhook-secret");

    public const int MaxBodyBytes = 256 * 1024;
    private const int MaxIdentifierLength = 64;
    private const string PayloadField = "raw_body";
    private static readonly SecretReference MasterKey = new("envelope-master-key");

    private readonly NpgsqlDataSource _dataSource;
    private readonly SecretResolver _secrets;
    private readonly SensitivePayloadProtector _protector;

    public YemeksepetiWebhookInbox(NpgsqlDataSource dataSource, ISecretProvider secretProvider)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        ArgumentNullException.ThrowIfNull(secretProvider);
        var policy = new YemeksepetiWebhookAccessPolicy();
        _secrets = new SecretResolver(secretProvider, policy);
        _protector = new SensitivePayloadProtector(new AesGcmEnvelopeCipher(_secrets), policy);
    }

    public async Task<WebhookReceipt> ReceiveAsync(
        string? authorizationHeader,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
    {
        string expected;
        try
        {
            using var secret = _secrets.Resolve(WebhookSecret, YemeksepetiWebhookAccessPolicy.Accessor);
            expected = secret.Value;
        }
        catch (SecretNotFoundException)
        {
            return new WebhookReceipt(WebhookReceiptOutcome.ChannelNotConfigured, null);
        }

        if (!Matches(authorizationHeader, expected))
            return new WebhookReceipt(WebhookReceiptOutcome.Unauthenticated, null);
        if (body.Length > MaxBodyBytes)
            return new WebhookReceipt(WebhookReceiptOutcome.TooLarge, null);
        if (!TryReadIdentity(body, out var orderId, out var status, out var updatedAt))
            return new WebhookReceipt(WebhookReceiptOutcome.Malformed, null);

        var eventKey = Hex(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', orderId, status, updatedAt ?? string.Empty))));
        var rawBody = Encoding.UTF8.GetString(body.Span);
        var envelope = _protector.Protect(
            new SensitivePayload(
                new Dictionary<string, string> { [PayloadField] = rawBody },
                new Dictionary<string, SensitiveCategory> { [PayloadField] = SensitiveCategory.Pii }),
            MasterKey,
            YemeksepetiWebhookAccessPolicy.Accessor);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (var insert = new NpgsqlCommand(
            """
            INSERT INTO online_ordering.yemeksepeti_webhook_inbox (
                inbox_id, event_key, external_order_id, provider_status, provider_updated_at, body_sha256, payload_envelope)
            VALUES ($1, $2, $3, $4, $5, $6, $7)
            ON CONFLICT (event_key) DO NOTHING
            RETURNING inbox_id;
            """, connection))
        {
            insert.Parameters.AddWithValue(Guid.NewGuid());
            insert.Parameters.AddWithValue(eventKey);
            insert.Parameters.AddWithValue(orderId);
            insert.Parameters.AddWithValue(status);
            insert.Parameters.AddWithValue((object?)updatedAt ?? DBNull.Value);
            insert.Parameters.AddWithValue(Hex(SHA256.HashData(body.Span)));
            insert.Parameters.AddWithValue(envelope.ToPersistenceBytes());
            if (await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is Guid stored)
                return new WebhookReceipt(WebhookReceiptOutcome.Stored, stored);
        }

        await using var existing = new NpgsqlCommand(
            "SELECT inbox_id FROM online_ordering.yemeksepeti_webhook_inbox WHERE event_key = $1;", connection);
        existing.Parameters.AddWithValue(eventKey);
        var inboxId = (Guid)(await existing.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        return new WebhookReceipt(WebhookReceiptOutcome.Duplicate, inboxId);
    }

    /// <summary>Compares SHA-256 digests in constant time, so neither content nor length of the secret leaks through timing.</summary>
    private static bool Matches(string? presented, string expected)
    {
        if (string.IsNullOrEmpty(presented))
            return false;
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(presented.Trim())),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected.Trim())));
    }

    private static bool TryReadIdentity(ReadOnlyMemory<byte> body, out string orderId, out string status, out string? updatedAt)
    {
        orderId = status = string.Empty;
        updatedAt = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !TryIdentifier(root, "order_id", out orderId)
                || !TryIdentifier(root, "status", out status))
                return false;

            if (root.TryGetProperty("sys", out var sys) && sys.ValueKind == JsonValueKind.Object
                && sys.TryGetProperty("updated_at", out var updated) && updated.ValueKind == JsonValueKind.String)
            {
                var value = updated.GetString()!.Trim();
                if (value.Length == 0 || value.Length > MaxIdentifierLength || value.Any(char.IsControl))
                    return false;
                updatedAt = value;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryIdentifier(JsonElement root, string name, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
            return false;
        var text = element.GetString()!.Trim();
        if (text.Length == 0 || text.Length > MaxIdentifierLength || text.Any(char.IsControl))
            return false;
        value = text;
        return true;
    }

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
