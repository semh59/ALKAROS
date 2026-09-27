using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.IntegrationContracts;
using ALKAROS.OnlineOrdering.Providers.Inbox;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.OrderIntake;
using ALKAROS.Secrets;
using Npgsql;

namespace ALKAROS.OnlineOrdering.Providers.TrendyolGo.StatusSync;

/// <summary>
/// V12-TGO-003: the calls that tell Trendyol Go about a package. UNVERIFIED DRAFT (EXT:TGO-MEAL-API, read 2026-09-27):
/// <list type="bullet">
/// <item><c>PUT {base}/integrator/order/meal/suppliers/{supplierId}/packages/picked</c> <c>{packageId, preparationTime}</c>;</item>
/// <item><c>PUT .../packages/invoiced</c> <c>{packageId, actualDate}</c> (epoch milliseconds, a past moment);</item>
/// <item><c>PUT .../packages/{packageId}/manual-shipped</c> <c>{actualDate}</c> (only for restaurants with their own couriers);</item>
/// <item><c>PUT .../packages/unsupplied</c> <c>{packageId, itemIdList, reasonId}</c>.</item>
/// </list>
/// Every call carries the documented headers (<see cref="TrendyolGoApiSettings.Apply"/>); the settings are read for every
/// call, so changed credentials are used at once after a 401. The document allows 50 requests per 10 seconds per
/// endpoint; this client waits rather than exceed it.
/// </summary>
public sealed class TrendyolGoStatusClient : IDisposable
{
    public const int RequestsPerWindow = 50;
    public static readonly TimeSpan RateWindow = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;
    private readonly ISecretProvider _secrets;
    private readonly TimeProvider _time;
    private readonly int _requestsPerWindow;
    private readonly TimeSpan _window;
    private readonly Dictionary<string, Queue<DateTimeOffset>> _sent = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <param name="requestsPerWindow">The documented 50 unless a test narrows it.</param>
    /// <param name="window">The documented 10 seconds unless a test narrows it.</param>
    public TrendyolGoStatusClient(
        HttpClient http, ISecretProvider secrets, TimeProvider time, int requestsPerWindow = RequestsPerWindow, TimeSpan? window = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        ArgumentOutOfRangeException.ThrowIfLessThan(requestsPerWindow, 1);
        _requestsPerWindow = requestsPerWindow;
        _window = window ?? RateWindow;
    }

    public Task PickedAsync(string packageId, int preparationMinutes, CancellationToken cancellationToken = default) =>
        PutAsync("picked", "packages/picked", new { packageId, preparationTime = preparationMinutes }, cancellationToken);

    /// <summary>
    /// V12-TGO-005: after the Uber Eats transition the platform may move a package to Invoiced by itself; the document
    /// says sending invoiced again must not fail, so a conflict answer here counts as already invoiced.
    /// </summary>
    public Task InvoicedAsync(string packageId, CancellationToken cancellationToken = default) =>
        PutAsync("invoiced", "packages/invoiced", new { packageId, actualDate = ActualDate() }, cancellationToken, conflictMeansDone: true);

    public Task ManualShippedAsync(string packageId, CancellationToken cancellationToken = default) =>
        PutAsync("manual-shipped", $"packages/{Uri.EscapeDataString(packageId)}/manual-shipped", new { actualDate = ActualDate() }, cancellationToken);

    public Task UnsuppliedAsync(string packageId, IReadOnlyList<string> itemIds, int reasonId, CancellationToken cancellationToken = default) =>
        PutAsync("unsupplied", "packages/unsupplied", new { packageId, itemIdList = itemIds, reasonId }, cancellationToken);

    public void Dispose()
    {
        _gate.Dispose();
        _http.Dispose();
    }

    /// <summary>"Must be a past date when provided": one second before now.</summary>
    private long ActualDate() => _time.GetUtcNow().AddSeconds(-1).ToUnixTimeMilliseconds();

    private async Task PutAsync(string endpoint, string path, object body, CancellationToken cancellationToken, bool conflictMeansDone = false)
    {
        var settings = TrendyolGoApiSettings.Resolve(_secrets)
            ?? throw new TrendyolGoApiException("Trendyol Go settings are not entered.");
        await WaitForRateAsync(endpoint, cancellationToken).ConfigureAwait(false);
        var uri = new Uri(settings.BaseUrl, string.Create(CultureInfo.InvariantCulture,
            $"integrator/order/meal/suppliers/{Uri.EscapeDataString(settings.SupplierId)}/{path}"));
        using var request = new HttpRequestMessage(HttpMethod.Put, uri) { Content = JsonContent.Create(body) };
        settings.Apply(request);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (conflictMeansDone && response.StatusCode == System.Net.HttpStatusCode.Conflict)
            return;
        if (!response.IsSuccessStatusCode)
            throw new TrendyolGoApiException($"Trendyol Go {endpoint} call failed with HTTP {(int)response.StatusCode}.");
    }

    /// <summary>At most the allowed number of calls to one endpoint in any window.</summary>
    private async Task WaitForRateAsync(string endpoint, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_sent.TryGetValue(endpoint, out var sent))
                _sent[endpoint] = sent = new Queue<DateTimeOffset>();
            var now = _time.GetUtcNow();
            while (sent.Count > 0 && now - sent.Peek() >= _window)
                sent.Dequeue();
            if (sent.Count >= _requestsPerWindow)
            {
                var wait = _window - (now - sent.Peek());
                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait, _time, cancellationToken).ConfigureAwait(false);
                sent.Dequeue();
            }

            sent.Enqueue(_time.GetUtcNow());
        }
        finally
        {
            _gate.Release();
        }
    }
}

/// <summary>
/// V12-TGO-003: delivers a committed local change to Trendyol Go; the outbox retries a failure and dead-letters it after
/// the provider retry budget, which reconciliation then shows.
/// </summary>
public sealed class TrendyolGoStatusUpdateConsumer : IIntegrationEventConsumer
{
    public const int DefaultPreparationMinutes = 20;
    public static readonly SecretReference PreparationMinutesReference = new("trendyol-go-preparation-minutes");

    private readonly TrendyolGoStatusClient _client;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ProviderInbox _inbox;
    private readonly ISecretProvider _secrets;

    public TrendyolGoStatusUpdateConsumer(TrendyolGoStatusClient client, NpgsqlDataSource dataSource, ProviderInbox inbox, ISecretProvider secrets)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
    }

    public bool CanHandle(string eventType) =>
        string.Equals(eventType, TrendyolGoStatusSync.StatusUpdateRequestedEventType, StringComparison.Ordinal);

    public async Task HandleAsync(string eventType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var update = IntegrationEventSerializer.Deserialize<TrendyolGoStatusUpdateRequested>(payload.Span);
        switch (update.Action)
        {
            case TrendyolGoPackageAction.Picked:
                await _client.PickedAsync(update.ExternalOrderId, PreparationMinutes(), cancellationToken).ConfigureAwait(false);
                break;
            case TrendyolGoPackageAction.Invoiced:
                await _client.InvoicedAsync(update.ExternalOrderId, cancellationToken).ConfigureAwait(false);
                break;
            case TrendyolGoPackageAction.InvoicedAndShipped:
                await _client.InvoicedAsync(update.ExternalOrderId, cancellationToken).ConfigureAwait(false);
                await _client.ManualShippedAsync(update.ExternalOrderId, cancellationToken).ConfigureAwait(false);
                break;
            case TrendyolGoPackageAction.Unsupplied:
                var items = await PackageItemIdsAsync(update.ExternalOrderId, cancellationToken).ConfigureAwait(false);
                await _client.UnsuppliedAsync(update.ExternalOrderId, items, update.ReasonId!.Value, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Unknown Trendyol Go package action '{update.Action}'.");
        }
    }

    /// <summary>The restaurant's preparation time setting in minutes (1–120), or <see cref="DefaultPreparationMinutes"/>.</summary>
    private int PreparationMinutes() =>
        _secrets.GetValue(PreparationMinutesReference) is { } text
        && int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && minutes is >= 1 and <= 120
            ? minutes
            : DefaultPreparationMinutes;

    /// <summary>The package's item ids (<c>packageItemId</c>, not already cancelled) from its stored <c>created</c> payload.</summary>
    private async Task<IReadOnlyList<string>> PackageItemIdsAsync(string packageId, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT payload_envelope FROM online_ordering.provider_inbox
            WHERE provider = $1 AND external_order_id = $2 AND provider_status = $3
            ORDER BY received_at LIMIT 1;
            """);
        command.Parameters.AddWithValue(TrendyolGoEvents.Provider);
        command.Parameters.AddWithValue(packageId);
        command.Parameters.AddWithValue(TrendyolGoEvents.Created);
        if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not byte[] envelope)
            throw new TrendyolGoApiException($"No stored package for '{packageId}'; its items are unknown.");

        using var document = JsonDocument.Parse(_inbox.OpenPayload(envelope));
        var ids = new List<string>();
        if (document.RootElement.TryGetProperty("lines", out var lines) && lines.ValueKind == JsonValueKind.Array)
        {
            foreach (var line in lines.EnumerateArray())
            {
                if (!line.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var item in items.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object
                        && !(item.TryGetProperty("isCancelled", out var cancelled) && cancelled.ValueKind == JsonValueKind.True)
                        && TrendyolGoOrderNormalizer.Identifier(item, "packageItemId") is { } id)
                        ids.Add(id);
                }
            }
        }

        return ids.Count > 0 ? ids : throw new TrendyolGoApiException($"Package '{packageId}' has no item to cancel.");
    }
}
