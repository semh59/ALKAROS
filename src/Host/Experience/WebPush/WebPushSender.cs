using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ALKAROS.Host.Experience.WebPush;

/// <summary>
/// V1-WTR-011: what the waiter's phone shows when the app is not open.
///
/// Deliberately best-effort. A notification that cannot be delivered must
/// never fail the thing it was announcing - an order still reaches the
/// kitchen if a push service is down - so every failure here is logged and
/// swallowed, and only a permanently dead subscription (404/410) changes any
/// state, by deleting itself.
/// </summary>
public sealed class WebPushSender
{
    // CA1848: the analyzer is enforced as an error in this repository, so
    // every message goes through a cached delegate, like the rest of Host.
    private static readonly Action<ILogger, Exception?> LogNotPrepared =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(5600, nameof(LogNotPrepared)),
            "Web push could not be prepared; the notification was not sent.");

    private static readonly Action<ILogger, int, Exception?> LogSubscriptionGone =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(5601, nameof(LogSubscriptionGone)),
            "Removing a push subscription the service reported as gone ({StatusCode}).");

    private static readonly Action<ILogger, int, Exception?> LogRejected =
        LoggerMessage.Define<int>(
            LogLevel.Warning,
            new EventId(5602, nameof(LogRejected)),
            "A web push was rejected by the push service with status {StatusCode}.");

    private static readonly Action<ILogger, Exception?> LogUndelivered =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(5603, nameof(LogUndelivered)),
            "A web push could not be delivered.");

    private readonly HttpClient _httpClient;
    private readonly PushSubscriptionStore _store;
    private readonly ILogger<WebPushSender> _logger;
    private readonly string _subject;

    public WebPushSender(
        HttpClient httpClient,
        PushSubscriptionStore store,
        ILogger<WebPushSender> logger,
        string subject = "mailto:destek@alkaros.local")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _subject = subject;
    }

    /// <summary>The public key a client needs before it can subscribe.</summary>
    public async Task<string> GetPublicKeyAsync(CancellationToken cancellationToken = default) =>
        (await _store.GetOrCreateVapidKeysAsync(_subject, cancellationToken)).PublicKey;

    /// <summary>
    /// Sends one notification to every registered device. Still the right
    /// call for an event with no single addressee (e.g. a pending QR order —
    /// nobody has claimed it yet, V1-RMD-149) or a legacy/unassigned order
    /// (V1-RMD-201: <c>Order.ServingUserId</c> is null).
    /// </summary>
    public Task BroadcastAsync(WebPushMessage message, CancellationToken cancellationToken = default)
        => SendToSubscriptionsAsync(message, _store.GetAllAsync, cancellationToken);

    /// <summary>
    /// V1-RMD-201: sends one notification to only the devices one specific
    /// user has subscribed from — used once an order's serving waiter is
    /// known, so the rest of the floor's phones stay quiet.
    /// </summary>
    public Task SendToUserAsync(WebPushMessage message, Guid userId, CancellationToken cancellationToken = default)
        => SendToSubscriptionsAsync(message, ct => _store.GetByUserAsync(userId, ct), cancellationToken);

    /// <summary>
    /// V1-RMD-203: a caller deciding between <see cref="SendToUserAsync"/>
    /// and <see cref="BroadcastAsync"/> needs to know this first — a user
    /// with zero subscriptions (e.g. staff who only ever use a client that
    /// never registers for push, like Cashier/PosTerminal) would otherwise
    /// silently receive nothing from a targeted send.
    /// </summary>
    public async Task<bool> HasAnySubscriptionAsync(Guid userId, CancellationToken cancellationToken = default)
        => (await _store.GetByUserAsync(userId, cancellationToken)).Count > 0;

    private async Task SendToSubscriptionsAsync(
        WebPushMessage message,
        Func<CancellationToken, Task<IReadOnlyList<PushSubscriptionRecord>>> loadSubscriptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        IReadOnlyList<PushSubscriptionRecord> subscriptions;
        VapidKeyPair keys;
        try
        {
            subscriptions = await loadSubscriptions(cancellationToken);
            if (subscriptions.Count == 0) return;
            keys = await _store.GetOrCreateVapidKeysAsync(_subject, cancellationToken);
        }
        catch (Exception ex)
        {
            LogNotPrepared(_logger, ex);
            return;
        }

        var payload = JsonSerializer.Serialize(message);
        foreach (var subscription in subscriptions)
        {
            await SendOneAsync(subscription, payload, keys, cancellationToken);
        }
    }

    private async Task SendOneAsync(
        PushSubscriptionRecord subscription, string payload, VapidKeyPair keys, CancellationToken cancellationToken)
    {
        try
        {
            var body = WebPushCrypto.Encrypt(payload, subscription.P256dh, subscription.Auth);
            var authorization = WebPushCrypto.CreateVapidAuthorizationHeader(
                WebPushCrypto.AudienceOf(subscription.Endpoint),
                keys.Subject,
                keys.PublicKey,
                keys.PrivateKey,
                DateTimeOffset.UtcNow);

            using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Endpoint);
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
            request.Headers.TryAddWithoutValidation("TTL", "600");
            request.Headers.TryAddWithoutValidation("Urgency", "high");
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content.Headers.ContentEncoding.Add("aes128gcm");

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                await _store.MarkSuccessAsync(subscription.Endpoint, cancellationToken);
                return;
            }

            // RFC 8030 §7.3: these two mean the subscription is gone for good.
            // Anything else may recover, so the row stays and only the counter
            // moves.
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                LogSubscriptionGone(_logger, (int)response.StatusCode, null);
                await _store.DeleteByEndpointAsync(subscription.Endpoint, cancellationToken);
                return;
            }

            LogRejected(_logger, (int)response.StatusCode, null);
            await _store.MarkFailureAsync(subscription.Endpoint, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException or ArgumentException)
        {
            // A push service that is unreachable, slow, or a subscription
            // whose stored keys no longer parse. None of it is worth failing
            // the caller over.
            LogUndelivered(_logger, ex);
        }
    }
}

/// <summary>
/// V1-WTR-011: what the service worker receives. Kept flat and small - the
/// whole encrypted body must fit inside a push service's 4096-byte
/// guarantee, and the device re-reads the authoritative state from the API
/// as soon as the waiter opens the app.
/// </summary>
public sealed record WebPushMessage(string Title, string Body, string Tag, string? Url = null);
