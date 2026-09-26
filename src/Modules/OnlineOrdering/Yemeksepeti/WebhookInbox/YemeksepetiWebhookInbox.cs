using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Npgsql;
using ALKAROS.OnlineOrdering.OrderLinks;

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
    public const int MaxCustomerNoteLength = 200;
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

    /// <summary>
    /// V12-RMD-007: the authentication step alone, so the endpoint can refuse a delivery before reading its body.
    /// Null when the delivery presents the configured secret; otherwise why it is refused.
    /// </summary>
    public WebhookReceiptOutcome? Authenticate(string? authorizationHeader)
    {
        string expected;
        try
        {
            using var secret = _secrets.Resolve(WebhookSecret, YemeksepetiWebhookAccessPolicy.Accessor);
            expected = secret.Value;
        }
        catch (SecretNotFoundException)
        {
            return WebhookReceiptOutcome.ChannelNotConfigured;
        }

        return Matches(authorizationHeader, expected) ? null : WebhookReceiptOutcome.Unauthenticated;
    }

    public async Task<WebhookReceipt> ReceiveAsync(
        string? authorizationHeader,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
    {
        if (Authenticate(authorizationHeader) is { } refused)
            return new WebhookReceipt(refused, null);
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
            INSERT INTO online_ordering.provider_inbox (
                inbox_id, event_key, external_order_id, provider_status, provider_updated_at, body_sha256, payload_envelope, provider)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
            ON CONFLICT (provider, event_key) DO NOTHING
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
            // V12-ONL-008: the inbox is shared by every platform; these rows are Yemeksepeti's.
            insert.Parameters.AddWithValue(OnlineOrderProviders.Yemeksepeti);
            if (await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is Guid stored)
                return new WebhookReceipt(WebhookReceiptOutcome.Stored, stored);
        }

        await using var existing = new NpgsqlCommand(
            "SELECT inbox_id FROM online_ordering.provider_inbox WHERE provider = $1 AND event_key = $2;", connection);
        existing.Parameters.AddWithValue(OnlineOrderProviders.Yemeksepeti);
        existing.Parameters.AddWithValue(eventKey);
        var inboxId = (Guid)(await existing.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        return new WebhookReceipt(WebhookReceiptOutcome.Duplicate, inboxId);
    }

    /// <summary>
    /// V12-ONL-002: opens a stored delivery's raw body for processing. The inbox stays the only
    /// component whose accessor can decrypt it.
    /// </summary>
    public string OpenPayload(byte[] payloadEnvelope)
    {
        ArgumentNullException.ThrowIfNull(payloadEnvelope);
        var payload = _protector.Unprotect(
            SensitiveEnvelope.FromPersistenceBytes(payloadEnvelope), MasterKey, YemeksepetiWebhookAccessPolicy.Accessor);
        return payload.Fields[PayloadField];
    }

    /// <summary>
    /// V12-RMD-007: the configured secret is the whole header value the provider sends (for example
    /// <c>Bearer abc123</c>). When both values carry a scheme, the scheme is compared case-insensitively (RFC 7235)
    /// and only the credential must match exactly; otherwise the whole values must match. The credential comparison
    /// is on SHA-256 digests in constant time, so neither content nor length of the secret leaks through timing.
    /// </summary>
    private static bool Matches(string? presented, string expected)
    {
        if (string.IsNullOrWhiteSpace(presented))
            return false;

        var (presentedScheme, presentedCredential) = Split(presented.Trim());
        var (expectedScheme, expectedCredential) = Split(expected.Trim());
        var schemeMatches = string.Equals(presentedScheme, expectedScheme, StringComparison.OrdinalIgnoreCase);
        var credentialMatches = CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(presentedCredential)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expectedCredential)));
        return schemeMatches & credentialMatches;

        static (string Scheme, string Credential) Split(string value)
        {
            var space = value.IndexOf(' ', StringComparison.Ordinal);
            return space < 0 ? (string.Empty, value) : (value[..space], value[(space + 1)..].TrimStart());
        }
    }

    /// <summary>
    /// V12-RMD-007: the customer's order-level note from a stored payload — control characters removed, bounded —
    /// or null. It is never copied into an order (it often carries a phone number or an address); it is opened only
    /// for an authorized, audited view.
    /// </summary>
    public string? ReadCustomerNote(byte[] payloadEnvelope)
    {
        using var document = JsonDocument.Parse(OpenPayload(payloadEnvelope));
        if (!document.RootElement.TryGetProperty("comment", out var comment) || comment.ValueKind != JsonValueKind.String)
            return null;
        var text = new string(comment.GetString()!.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return text.Length == 0 ? null : text[..Math.Min(MaxCustomerNoteLength, text.Length)];
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
