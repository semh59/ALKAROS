using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.OrderIntake;
using ALKAROS.Secrets;

namespace ALKAROS.OnlineOrdering.Providers.TrendyolGo.Menu;

/// <summary>One product price for <c>POST .../products/price</c>.</summary>
public sealed record TrendyolGoPriceUpdate(long ProductId, decimal SellingPrice);

/// <summary>A price update batch as <c>GET .../batch-requests/{id}</c> answers it.</summary>
public sealed record TrendyolGoBatchResult(string Status, int FailedItemCount, IReadOnlyList<(long ProductId, string Reason)> Failures);

public sealed class TrendyolGoMenuException : Exception
{
    public TrendyolGoMenuException(string message) : base(message) { }
}

/// <summary>
/// V12-TGO-004: Trendyol Go's menu calls. UNVERIFIED DRAFT (EXT:TGO-MEAL-API "Menu Integration", read 2026-09-27;
/// V12-TGO-001 Blocked, C106 waiver):
/// <list type="bullet">
/// <item><c>GET {base}/integrator/product/meal/suppliers/{supplierId}/stores/{storeId}/products</c>: the menu
/// (<c>products[].id</c>, <c>status</c>);</item>
/// <item><c>PUT .../stores/{storeId}/products/{productId}/status</c> <c>{"status":"ACTIVE"|"PASSIVE"}</c>; a 409 means a
/// concurrent change of the same store: sent once more;</item>
/// <item><c>POST .../suppliers/{supplierId}/products/price</c> <c>{"items":[{productId, sellingPrice}]}</c>, at most 1000
/// items, answering <c>batchRequestId</c>; an unchanged repeat is refused by the platform;</item>
/// <item><c>GET .../suppliers/{supplierId}/batch-requests/{batchRequestId}</c>: <c>status</c>, <c>failedItemCount</c>,
/// <c>items[].status/failureReasons/requestItem.productId</c> (kept for 4 hours).</item>
/// </list>
/// The store id is the platform setting <c>store-id</c>; the other settings and headers are the shared ones.
/// </summary>
public sealed class TrendyolGoMenuClient
{
    public const int MaxPriceItems = 1000;
    public static readonly SecretReference StoreIdReference = new("trendyol-go-store-id");

    private readonly HttpClient _http;
    private readonly ISecretProvider _secrets;
    private readonly TimeProvider _time;

    public TrendyolGoMenuClient(HttpClient http, ISecretProvider secrets, TimeProvider time)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <summary>Whether every setting the menu calls need is entered.</summary>
    public bool IsConfigured => TrendyolGoApiSettings.Resolve(_secrets) is not null && StoreId() is not null;

    /// <summary>The platform product ids on the store's menu.</summary>
    public async Task<IReadOnlySet<long>> MenuProductIdsAsync(CancellationToken cancellationToken = default)
    {
        var (settings, storeId) = Require();
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint(settings, $"stores/{Uri.EscapeDataString(storeId)}/products"));
        settings.Apply(request);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureAsync(response, "menu read", cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false));
        if (!document.RootElement.TryGetProperty("products", out var products) || products.ValueKind != JsonValueKind.Array)
            throw new TrendyolGoMenuException("The menu answer has no products array.");
        return products.EnumerateArray()
            .Where(p => p.ValueKind == JsonValueKind.Object && p.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number)
            .Select(p => p.GetProperty("id").GetInt64())
            .ToHashSet();
    }

    /// <summary>Puts one product on sale or takes it off; a concurrent-change 409 is tried once more.</summary>
    public async Task SetProductActiveAsync(long productId, bool active, CancellationToken cancellationToken = default)
    {
        var (settings, storeId) = Require();
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, Endpoint(settings,
                string.Create(CultureInfo.InvariantCulture, $"stores/{Uri.EscapeDataString(storeId)}/products/{productId}/status")))
            {
                Content = JsonContent.Create(new { status = active ? "ACTIVE" : "PASSIVE" })
            };
            settings.Apply(request);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Conflict && attempt == 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), _time, cancellationToken).ConfigureAwait(false);
                continue;
            }

            await EnsureAsync(response, "product status", cancellationToken).ConfigureAwait(false);
            return;
        }
    }

    /// <summary>Queues a price update; returns the batch request id.</summary>
    public async Task<string> UpdatePricesAsync(IReadOnlyList<TrendyolGoPriceUpdate> prices, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prices);
        if (prices.Count is 0 or > MaxPriceItems)
            throw new ArgumentException($"A price update carries 1-{MaxPriceItems} products.", nameof(prices));
        var (settings, _) = Require();
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint(settings, "products/price"))
        {
            Content = JsonContent.Create(new { items = prices.Select(p => new { productId = p.ProductId, sellingPrice = p.SellingPrice }) })
        };
        settings.Apply(request);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureAsync(response, "price update", cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false));
        return document.RootElement.TryGetProperty("batchRequestId", out var id) && id.ValueKind == JsonValueKind.String
               && !string.IsNullOrWhiteSpace(id.GetString())
            ? id.GetString()!
            : throw new TrendyolGoMenuException("The price update answer has no batchRequestId.");
    }

    public async Task<TrendyolGoBatchResult> BatchResultAsync(string batchRequestId, CancellationToken cancellationToken = default)
    {
        var (settings, _) = Require();
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint(settings, $"batch-requests/{Uri.EscapeDataString(batchRequestId)}"));
        settings.Apply(request);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureAsync(response, "batch result", cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false));
        var root = document.RootElement;
        var status = root.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString()! : "";
        var failedCount = root.TryGetProperty("failedItemCount", out var f) && f.ValueKind == JsonValueKind.Number ? f.GetInt32() : 0;
        var failures = new List<(long, string)>();
        if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;
                // Only a row the platform refused counts: FAILED, or any failure reason. A row still being processed does not.
                var itemFailed = item.TryGetProperty("status", out var itemStatus) && itemStatus.ValueKind == JsonValueKind.String
                                 && itemStatus.GetString() == "FAILED";
                var hasReasons = item.TryGetProperty("failureReasons", out var reasonList) && reasonList.ValueKind == JsonValueKind.Array
                                 && reasonList.GetArrayLength() > 0;
                if (!itemFailed && !hasReasons)
                    continue;
                var productId = item.TryGetProperty("requestItem", out var requestItem) && requestItem.ValueKind == JsonValueKind.Object
                                && requestItem.TryGetProperty("productId", out var pid) && pid.ValueKind == JsonValueKind.Number
                    ? pid.GetInt64()
                    : 0L;
                var reason = item.TryGetProperty("failureReasons", out var reasons) && reasons.ValueKind == JsonValueKind.Array
                    ? string.Join("; ", reasons.EnumerateArray().Select(r => r.ValueKind == JsonValueKind.String ? r.GetString() : r.GetRawText()))
                    : "";
                failures.Add((productId, reason));
            }
        }

        return new TrendyolGoBatchResult(status, failedCount, failures);
    }

    private string? StoreId() => _secrets.GetValue(StoreIdReference) is { } value && value.Trim().Length > 0 ? value.Trim() : null;

    private (TrendyolGoApiSettings Settings, string StoreId) Require() =>
        (TrendyolGoApiSettings.Resolve(_secrets) ?? throw new TrendyolGoMenuException("Trendyol Go settings are not entered."),
         StoreId() ?? throw new TrendyolGoMenuException("The Trendyol Go store id is not entered."));

    private static Uri Endpoint(TrendyolGoApiSettings settings, string path) =>
        new(settings.BaseUrl, $"integrator/product/meal/suppliers/{Uri.EscapeDataString(settings.SupplierId)}/{path}");

    private static async Task EnsureAsync(HttpResponseMessage response, string call, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;
        // The platform's own message is kept for the publication's error (never shown raw to staff).
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var message = "";
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                message = string.Join("; ", errors.EnumerateArray()
                    .Select(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("message", out var m) ? m.GetString() : null)
                    .Where(m => !string.IsNullOrWhiteSpace(m)));
        }
        catch (JsonException)
        {
        }

        throw new TrendyolGoMenuException($"Trendyol Go {call} failed with HTTP {(int)response.StatusCode}{(message.Length == 0 ? "" : ": " + message)}.");
    }
}
