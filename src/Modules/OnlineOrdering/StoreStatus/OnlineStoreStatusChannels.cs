using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.Menu;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.OrderIntake;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;
using ALKAROS.Secrets;

namespace ALKAROS.OnlineOrdering.StoreStatus;

/// <summary>Whether the restaurant takes orders on a platform.</summary>
public enum OnlineStoreState
{
    Open,

    /// <summary>Closed for the rest of the service day (until the next 06:00, Europe/Istanbul).</summary>
    ClosedToday,

    /// <summary>Closed until a given time (the "busy" pause).</summary>
    ClosedUntil
}

public enum OnlineStoreCloseReason
{
    Busy,
    Closed
}

/// <summary>What the platform itself reports.</summary>
public sealed record PlatformStoreState(bool Open, DateTimeOffset? ClosedUntil);

/// <summary>V12-ONL-011: one platform's open/closed switch.</summary>
public interface IOnlineStoreStatusChannel
{
    string Provider { get; }

    /// <summary>False while the platform's settings are not entered; nothing is sent then.</summary>
    bool IsConfigured { get; }

    /// <summary>True when the platform reopens by itself at a closing time; otherwise ALKAROS opens it again.</summary>
    bool ReopensByItself { get; }

    Task ApplyAsync(OnlineStoreState state, DateTimeOffset? until, OnlineStoreCloseReason? reason, CancellationToken cancellationToken = default);

    Task<PlatformStoreState> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// V12-ONL-011: Yemeksepeti's vendor status (UNVERIFIED DRAFT, EXT:YSP-PARTNER-2.0.2 "Outlet Management"):
/// <c>OPEN</c>, <c>CLOSED_TODAY</c>, and <c>CLOSED_UNTIL</c> with its time, which the platform ends by itself; a busy
/// pause is closed for <c>TOO_BUSY_KITCHEN</c>, any other closure for <c>CLOSED</c>.
/// </summary>
public sealed class YemeksepetiStoreStatusChannel(IYemeksepetiPartnerClient client, ISecretProvider secrets) : IOnlineStoreStatusChannel
{
    private readonly IYemeksepetiPartnerClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly ISecretProvider _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));

    public string Provider => "yemeksepeti";

    public bool IsConfigured =>
        new[] { YemeksepetiPartnerHttpClient.BaseUrl, YemeksepetiPartnerHttpClient.ChainId, YemeksepetiPartnerHttpClient.VendorId,
                YemeksepetiPartnerHttpClient.ClientId, YemeksepetiPartnerHttpClient.ClientSecret }
            .All(reference => _secrets.GetValue(reference) is not null);

    public bool ReopensByItself => true;

    public Task ApplyAsync(OnlineStoreState state, DateTimeOffset? until, OnlineStoreCloseReason? reason, CancellationToken cancellationToken = default)
    {
        var closedReason = reason == OnlineStoreCloseReason.Busy ? "TOO_BUSY_KITCHEN" : "CLOSED";
        return _client.SetVendorStatusAsync(state switch
        {
            OnlineStoreState.Open => new YemeksepetiVendorStatusChange("OPEN", null, null),
            OnlineStoreState.ClosedToday => new YemeksepetiVendorStatusChange("CLOSED_TODAY", closedReason, null),
            OnlineStoreState.ClosedUntil => new YemeksepetiVendorStatusChange("CLOSED_UNTIL", closedReason,
                until ?? throw new ArgumentException("A timed closure needs its time.", nameof(until))),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        }, cancellationToken);
    }

    public async Task<PlatformStoreState> ReadAsync(CancellationToken cancellationToken = default)
    {
        var status = await _client.GetVendorStatusAsync(cancellationToken).ConfigureAwait(false);
        return new PlatformStoreState(status.Status == "OPEN", status.ClosedUntil);
    }
}

/// <summary>
/// V12-ONL-011: Trendyol Go's restaurant working status (UNVERIFIED DRAFT, EXT:TGO-MEAL-API "Restaurant Integration"):
/// <c>PUT {base}/integrator/store/meal/suppliers/{supplierId}/stores/{storeId}/status</c> <c>OPEN</c>/<c>CLOSED</c>, and
/// the store's <c>workingStatus</c> in <c>GET .../suppliers/{supplierId}/stores</c>. The platform has no closing time, so
/// ALKAROS opens it again when a closure ends.
/// </summary>
public sealed class TrendyolGoStoreStatusChannel(HttpClient http, ISecretProvider secrets) : IOnlineStoreStatusChannel
{
    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));
    private readonly ISecretProvider _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));

    public string Provider => TrendyolGoEvents.Provider;

    public bool IsConfigured => TrendyolGoApiSettings.Resolve(_secrets) is not null && StoreId() is not null;

    public bool ReopensByItself => false;

    public async Task ApplyAsync(OnlineStoreState state, DateTimeOffset? until, OnlineStoreCloseReason? reason, CancellationToken cancellationToken = default)
    {
        var (settings, storeId) = Require();
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(settings.BaseUrl,
            $"integrator/store/meal/suppliers/{Uri.EscapeDataString(settings.SupplierId)}/stores/{Uri.EscapeDataString(storeId)}/status"))
        {
            Content = JsonContent.Create(new { status = state == OnlineStoreState.Open ? "OPEN" : "CLOSED" })
        };
        settings.Apply(request);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new TrendyolGoApiException($"Trendyol Go store status update failed with HTTP {(int)response.StatusCode}.");
    }

    public async Task<PlatformStoreState> ReadAsync(CancellationToken cancellationToken = default)
    {
        var (settings, storeId) = Require();
        for (var page = 0; page < 20; page++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(settings.BaseUrl, string.Create(CultureInfo.InvariantCulture,
                $"integrator/store/meal/suppliers/{Uri.EscapeDataString(settings.SupplierId)}/stores?page={page}&size=50")));
            settings.Apply(request);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new TrendyolGoApiException($"Trendyol Go store read failed with HTTP {(int)response.StatusCode}.");
            using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false));
            var root = document.RootElement;
            if (!root.TryGetProperty("restaurants", out var restaurants) || restaurants.ValueKind != JsonValueKind.Array)
                throw new TrendyolGoApiException("The store list answer has no restaurants array.");
            foreach (var restaurant in restaurants.EnumerateArray())
            {
                if (TrendyolGoOrderNormalizer.Identifier(restaurant, "id") == storeId)
                    return new PlatformStoreState(
                        restaurant.TryGetProperty("workingStatus", out var status) && status.ValueKind == JsonValueKind.String && status.GetString() == "OPEN",
                        null);
            }

            var totalPages = root.TryGetProperty("totalPages", out var total) && total.ValueKind == JsonValueKind.Number ? total.GetInt32() : 1;
            if (page + 1 >= totalPages)
                break;
        }

        throw new TrendyolGoApiException("The configured store is not in the seller's store list.");
    }

    private string? StoreId() => _secrets.GetValue(TrendyolGoMenuClient.StoreIdReference)?.Trim() is { Length: > 0 } id ? id : null;

    private (TrendyolGoApiSettings Settings, string StoreId) Require() =>
        (TrendyolGoApiSettings.Resolve(_secrets) ?? throw new TrendyolGoApiException("Trendyol Go settings are not entered."),
         StoreId() ?? throw new TrendyolGoApiException("The Trendyol Go store id is not entered."));
}
