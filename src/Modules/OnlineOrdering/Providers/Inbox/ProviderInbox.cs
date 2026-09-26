using System.Security.Cryptography;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Npgsql;

namespace ALKAROS.OnlineOrdering.Providers.Inbox;

/// <summary>
/// One platform event to keep: <see cref="EventKey"/> is computed by the platform's adapter from what identifies the
/// event (so a webhook delivery and a polled copy of the same event carry the same key); <see cref="RawBody"/> is
/// the platform's payload as received.
/// </summary>
public sealed record ProviderInboxEvent(
    string Provider,
    string EventKey,
    string ExternalOrderId,
    string ProviderStatus,
    string? ProviderUpdatedAt,
    ReadOnlyMemory<byte> RawBody);

public enum ProviderInboxStoreOutcome
{
    Stored,

    /// <summary>The same platform already has an event with this key; nothing new was stored.</summary>
    Duplicate
}

public sealed record ProviderInboxReceipt(ProviderInboxStoreOutcome Outcome, Guid InboxId);

/// <summary>
/// V12-ONL-010: the one writer and reader of <c>online_ordering.provider_inbox</c> payloads for every platform.
/// A payload is kept only as an AES-256-GCM envelope (field <c>raw_body</c>, category Pii, the envelope master
/// key), exactly the shape the Yemeksepeti webhook inbox wrote before, so events stored earlier open here too.
/// Its access policy is its own and is not registered as the shared policy singleton.
/// </summary>
public sealed class ProviderInbox
{
    public const int MaxIdentifierLength = 64;
    private const string PayloadField = "raw_body";
    private static readonly SecretReference MasterKey = new("envelope-master-key");

    private readonly NpgsqlDataSource _dataSource;
    private readonly SensitivePayloadProtector _protector;

    public ProviderInbox(NpgsqlDataSource dataSource, ISecretProvider secretProvider)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        ArgumentNullException.ThrowIfNull(secretProvider);
        var policy = new ProviderInboxAccessPolicy();
        _protector = new SensitivePayloadProtector(new AesGcmEnvelopeCipher(new SecretResolver(secretProvider, policy)), policy);
    }

    /// <summary>Stores <paramref name="inboxEvent"/> once per platform and event key.</summary>
    public async Task<ProviderInboxReceipt> StoreAsync(ProviderInboxEvent inboxEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inboxEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(inboxEvent.Provider);
        RequireIdentifier(inboxEvent.ExternalOrderId, nameof(inboxEvent.ExternalOrderId));
        RequireIdentifier(inboxEvent.ProviderStatus, nameof(inboxEvent.ProviderStatus));
        if (inboxEvent.ProviderUpdatedAt is not null)
            RequireIdentifier(inboxEvent.ProviderUpdatedAt, nameof(inboxEvent.ProviderUpdatedAt));
        if (inboxEvent.EventKey.Length != 64 || !inboxEvent.EventKey.All(char.IsAsciiHexDigitLower))
            throw new ArgumentException("An event key is a lowercase SHA-256 hex digest.", nameof(inboxEvent));

        var envelope = _protector.Protect(
            new SensitivePayload(
                new Dictionary<string, string> { [PayloadField] = System.Text.Encoding.UTF8.GetString(inboxEvent.RawBody.Span) },
                new Dictionary<string, SensitiveCategory> { [PayloadField] = SensitiveCategory.Pii }),
            MasterKey,
            ProviderInboxAccessPolicy.Accessor);

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
            insert.Parameters.AddWithValue(inboxEvent.EventKey);
            insert.Parameters.AddWithValue(inboxEvent.ExternalOrderId);
            insert.Parameters.AddWithValue(inboxEvent.ProviderStatus);
            insert.Parameters.AddWithValue((object?)inboxEvent.ProviderUpdatedAt ?? DBNull.Value);
            insert.Parameters.AddWithValue(Convert.ToHexString(SHA256.HashData(inboxEvent.RawBody.Span)).ToLowerInvariant());
            insert.Parameters.AddWithValue(envelope.ToPersistenceBytes());
            insert.Parameters.AddWithValue(inboxEvent.Provider);
            if (await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is Guid stored)
                return new ProviderInboxReceipt(ProviderInboxStoreOutcome.Stored, stored);
        }

        await using var existing = new NpgsqlCommand(
            "SELECT inbox_id FROM online_ordering.provider_inbox WHERE provider = $1 AND event_key = $2;", connection);
        existing.Parameters.AddWithValue(inboxEvent.Provider);
        existing.Parameters.AddWithValue(inboxEvent.EventKey);
        var inboxId = (Guid)(await existing.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        return new ProviderInboxReceipt(ProviderInboxStoreOutcome.Duplicate, inboxId);
    }

    /// <summary>Opens a stored event's raw payload for processing.</summary>
    public string OpenPayload(byte[] payloadEnvelope)
    {
        ArgumentNullException.ThrowIfNull(payloadEnvelope);
        var payload = _protector.Unprotect(
            SensitiveEnvelope.FromPersistenceBytes(payloadEnvelope), MasterKey, ProviderInboxAccessPolicy.Accessor);
        return payload.Fields[PayloadField];
    }

    private static void RequireIdentifier(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxIdentifierLength || value.Any(char.IsControl))
            throw new ArgumentException($"'{name}' must be 1-{MaxIdentifierLength} characters without control characters.", name);
    }
}

/// <summary>Only the shared inbox may resolve the envelope master key and open inbox payloads.</summary>
public sealed class ProviderInboxAccessPolicy : ISecretAccessPolicy, ISensitiveDataAccessPolicy
{
    public const string Accessor = "online-ordering.provider-inbox";

    public bool IsAllowed(string accessor, SecretReference reference) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);

    public bool CanRead(string accessor, SensitiveEnvelope envelope) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);
}
