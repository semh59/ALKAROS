using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.OnlineOrdering.Providers.Inbox;
using ALKAROS.Secrets;

namespace ALKAROS.OnlineOrdering.Providers.TrendyolGo.OrderIntake;

public enum TrendyolGoWebhookOutcome
{
    Stored,
    Duplicate,

    /// <summary>No webhook secret is configured, so the channel is closed; nothing is read or stored.</summary>
    ChannelNotConfigured,

    /// <summary>The delivery did not present the configured header value; nothing is stored.</summary>
    Unauthenticated,

    /// <summary>The path names no documented event type; nothing is stored.</summary>
    UnknownEventType,

    TooLarge,

    /// <summary>The body is not a JSON object with a package <c>id</c>; nothing is stored.</summary>
    Malformed
}

/// <summary>
/// V12-TGO-002. Authenticates one Trendyol Go webhook delivery and stores it once in the shared inbox. UNVERIFIED
/// DRAFT (EXT:TGO-MEAL-API "Create Integrator", read 2026-09-27): the integrator registers custom HTTP headers that
/// Trendyol Go sends on every call; this restaurant registers <see cref="HeaderName"/> with the value stored as the
/// platform's <c>webhook-secret</c>. Deliveries are POSTs to one path per event type, retried up to 3 times, and may
/// occasionally repeat — the inbox key makes a repeat a duplicate.
/// </summary>
public sealed class TrendyolGoWebhookInbox
{
    public const string HeaderName = "x-api-key";
    public const int MaxBodyBytes = 256 * 1024;
    public static readonly SecretReference WebhookSecret = new("trendyol-go-webhook-secret");

    private readonly SecretResolver _secrets;
    private readonly ProviderInbox _inbox;

    public TrendyolGoWebhookInbox(ProviderInbox inbox, ISecretProvider secretProvider)
    {
        _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
        ArgumentNullException.ThrowIfNull(secretProvider);
        _secrets = new SecretResolver(secretProvider, new WebhookSecretPolicy());
    }

    /// <summary>Null when the delivery presents the configured value; otherwise why it is refused. Nothing is read first.</summary>
    public TrendyolGoWebhookOutcome? Authenticate(string? presented)
    {
        string expected;
        try
        {
            using var secret = _secrets.Resolve(WebhookSecret, WebhookSecretPolicy.Accessor);
            expected = secret.Value;
        }
        catch (SecretNotFoundException)
        {
            return TrendyolGoWebhookOutcome.ChannelNotConfigured;
        }

        if (string.IsNullOrEmpty(presented))
            return TrendyolGoWebhookOutcome.Unauthenticated;
        // Digests compared in constant time: neither content nor length of the value leaks through timing.
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(presented.Trim())), SHA256.HashData(Encoding.UTF8.GetBytes(expected.Trim())))
            ? null
            : TrendyolGoWebhookOutcome.Unauthenticated;
    }

    public async Task<(TrendyolGoWebhookOutcome Outcome, Guid? InboxId)> ReceiveAsync(
        string eventType, string? presented, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
    {
        if (Authenticate(presented) is { } refused)
            return (refused, null);
        if (eventType is null || !TrendyolGoEvents.WebhookEventTypes.Contains(eventType))
            return (TrendyolGoWebhookOutcome.UnknownEventType, null);
        if (body.Length > MaxBodyBytes)
            return (TrendyolGoWebhookOutcome.TooLarge, null);
        if (ReadPackage(body) is not { } package)
            return (TrendyolGoWebhookOutcome.Malformed, null);

        var receipt = await _inbox.StoreAsync(
            new ProviderInboxEvent(
                TrendyolGoEvents.Provider, TrendyolGoEvents.EventKey(eventType, package.Id), package.Id, eventType, package.Timestamp, body),
            cancellationToken).ConfigureAwait(false);
        return (receipt.Outcome == ProviderInboxStoreOutcome.Stored ? TrendyolGoWebhookOutcome.Stored : TrendyolGoWebhookOutcome.Duplicate,
            receipt.InboxId);
    }

    private static (string Id, string? Timestamp)? ReadPackage(ReadOnlyMemory<byte> body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || TrendyolGoOrderNormalizer.Identifier(root, "id") is not { } id)
                return null;
            return (id, TrendyolGoOrderNormalizer.Identifier(root, "timestamp"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Only this inbox may resolve the Trendyol Go webhook secret.</summary>
    private sealed class WebhookSecretPolicy : ISecretAccessPolicy
    {
        public const string Accessor = "online-ordering.trendyol-go-webhook-inbox";

        public bool IsAllowed(string accessor, SecretReference reference) =>
            string.Equals(accessor, Accessor, StringComparison.Ordinal) && string.Equals(reference.Name, WebhookSecret.Name, StringComparison.Ordinal);
    }
}
