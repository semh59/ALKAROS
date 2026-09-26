using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ALKAROS.Secrets;

namespace ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;

/// <summary>The one provider call this channel makes today: telling Yemeksepeti an order's new status.</summary>
public interface IYemeksepetiPartnerClient
{
    Task UpdateOrderStatusAsync(YemeksepetiStatusUpdateRequested update, CancellationToken cancellationToken = default);

    /// <summary>
    /// V12-ONL-004/005: updates price and/or availability of products the vendor's catalog already has.
    /// Returns the provider's bulk-job identifier when the response carries one.
    /// </summary>
    Task<string?> UpdateVendorCatalogAsync(IReadOnlyList<YemeksepetiCatalogProductUpdate> products, CancellationToken cancellationToken = default);
}

/// <summary>One product line of a vendor catalog update: <c>sku</c> plus at least one of price, active, quantity.</summary>
public sealed record YemeksepetiCatalogProductUpdate(string Sku, decimal? Price, bool? Active, decimal? Quantity);

public sealed class YemeksepetiPartnerApiException : Exception
{
    public YemeksepetiPartnerApiException(string message) : base(message) { }
}

/// <summary>
/// UNVERIFIED DRAFT — written only from the public Partner API v2.0.2 page
/// (developer.yemeksepeti.com/api-specifications, read 2026-09-26) and never run against the
/// sandbox or production (no Partner Portal credential exists; V0-YSP-001 is Blocked):
/// <list type="bullet">
/// <item>token: <c>POST {base}/v2/oauth/token</c>, <c>application/x-www-form-urlencoded</c>,
/// <c>grant_type=client_credentials</c>, <c>client_id</c>, <c>client_secret</c>; response
/// <c>access_token</c>, <c>expires_in</c> (seconds); reused until shortly before expiry;</item>
/// <item>update: <c>PUT {base}/v2/chains/{chain_id}/orders/{order_id}</c> with <c>order_id</c>,
/// <c>status</c>, <c>items</c> (each needs <c>sku</c> and <c>status</c>) and, for a cancellation,
/// <c>cancellation.reason</c>.</item>
/// </list>
/// A second public copy of the document (partner-api-docs-tmp S3) disagrees on the base URL,
/// path and on <c>cancellation</c> being a plain string; this follows the developer portal page,
/// the source registered as EXT:YSP-PARTNER-2.0.2. The item <c>status</c> value <c>IN_CART</c>
/// is taken from that document's request sample only.
/// </summary>
public sealed class YemeksepetiPartnerHttpClient : IYemeksepetiPartnerClient, IDisposable
{
    public const string Accessor = "online-ordering.yemeksepeti-partner-client";

    public static readonly SecretReference BaseUrl = new("yemeksepeti-api-base-url");
    public static readonly SecretReference ChainId = new("yemeksepeti-chain-id");
    public static readonly SecretReference ClientId = new("yemeksepeti-client-id");
    public static readonly SecretReference ClientSecret = new("yemeksepeti-client-secret");
    public static readonly SecretReference VendorId = new("yemeksepeti-vendor-id");

    private static readonly TimeSpan ExpirySafetyMargin = TimeSpan.FromSeconds(60);

    private readonly HttpClient _http;
    private readonly SecretResolver _secrets;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private (string Token, DateTimeOffset ExpiresAt)? _token;

    public YemeksepetiPartnerHttpClient(HttpClient http, ISecretProvider secretProvider, TimeProvider time)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        ArgumentNullException.ThrowIfNull(secretProvider);
        _secrets = new SecretResolver(secretProvider, new PartnerClientSecretPolicy());
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public async Task UpdateOrderStatusAsync(YemeksepetiStatusUpdateRequested update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        var baseUrl = new Uri(Resolve(BaseUrl).TrimEnd('/') + "/");
        var chainId = Resolve(ChainId);
        var token = await AccessTokenAsync(baseUrl, cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            new Uri(baseUrl, $"v2/chains/{Uri.EscapeDataString(chainId)}/orders/{Uri.EscapeDataString(update.ExternalOrderId)}"))
        {
            Content = JsonContent.Create(new OrderUpdateBody(
                update.ExternalOrderId,
                StatusValue(update.Status),
                update.Items.Select(i => new OrderItemBody(i.Sku, "IN_CART", new OrderItemPricing(i.Quantity))).ToList(),
                update.Reason is { } reason ? new CancellationBody(ReasonValue(reason)) : null))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            await ForgetRejectedTokenAsync(response, token).ConfigureAwait(false);
            throw new YemeksepetiPartnerApiException(
                $"Yemeksepeti order update for '{update.ExternalOrderId}' failed with HTTP {(int)response.StatusCode}.");
        }
    }

    /// <summary>
    /// UNVERIFIED DRAFT (same source as above): <c>PUT {base}/v2/chains/{chain_id}/vendors/{vendor_id}/catalog</c>
    /// with <c>products</c>, each carrying the mandatory <c>sku</c> and at least one of <c>price</c>, <c>active</c>,
    /// <c>quantity</c>; bulk operations answer with a <c>job_id</c>. Only products the vendor catalog already has
    /// can be updated this way; creating products is a pilot-only beta call this client does not make.
    /// </summary>
    public async Task<string?> UpdateVendorCatalogAsync(
        IReadOnlyList<YemeksepetiCatalogProductUpdate> products, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(products);
        if (products.Count == 0)
            throw new ArgumentException("A catalog update needs at least one product.", nameof(products));
        if (products.Any(p => p.Price is null && p.Active is null && p.Quantity is null))
            throw new ArgumentException("Every product needs a price, an active flag or a quantity.", nameof(products));

        var baseUrl = new Uri(Resolve(BaseUrl).TrimEnd('/') + "/");
        var path = $"v2/chains/{Uri.EscapeDataString(Resolve(ChainId))}/vendors/{Uri.EscapeDataString(Resolve(VendorId))}/catalog";
        var token = await AccessTokenAsync(baseUrl, cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(baseUrl, path))
        {
            Content = JsonContent.Create(new CatalogUpdateBody(products
                .Select(p => new CatalogProductBody(p.Sku, p.Price, p.Active, p.Quantity))
                .ToList()))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            await ForgetRejectedTokenAsync(response, token).ConfigureAwait(false);
            throw new YemeksepetiPartnerApiException($"Yemeksepeti catalog update failed with HTTP {(int)response.StatusCode}.");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body))
            return null;
        using var document = JsonDocument.Parse(body);
        return document.RootElement.ValueKind == JsonValueKind.Object
               && document.RootElement.TryGetProperty("job_id", out var jobId)
               && jobId.ValueKind == JsonValueKind.String
            ? jobId.GetString()
            : null;
    }

    /// <summary>The client owns its HttpClient (created for it at registration) and its token lock.</summary>
    public void Dispose()
    {
        _tokenLock.Dispose();
        _http.Dispose();
    }

    private async Task<string> AccessTokenAsync(Uri baseUrl, CancellationToken cancellationToken)
    {
        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_token is { } cached && cached.ExpiresAt > _time.GetUtcNow())
                return cached.Token;

            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = Resolve(ClientId),
                ["client_secret"] = Resolve(ClientSecret)
            });
            using var response = await _http.PostAsync(new Uri(baseUrl, "v2/oauth/token"), form, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new YemeksepetiPartnerApiException($"Yemeksepeti token request failed with HTTP {(int)response.StatusCode}.");

            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken).ConfigureAwait(false);
            if (body is not { AccessToken: { Length: > 0 } accessToken, ExpiresIn: > 0 })
                throw new YemeksepetiPartnerApiException("Yemeksepeti token response had no usable access_token/expires_in.");

            _token = (accessToken, _time.GetUtcNow() + TimeSpan.FromSeconds(body.ExpiresIn) - ExpirySafetyMargin);
            return accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>
    /// V12-RMD-008: a 401 means the provider no longer accepts the cached token (revoked or expired early), so it
    /// is dropped and the next attempt fetches a new one — unless another call already replaced it.
    /// </summary>
    private async Task ForgetRejectedTokenAsync(HttpResponseMessage response, string token)
    {
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return;
        await _tokenLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (_token is { } cached && string.Equals(cached.Token, token, StringComparison.Ordinal))
                _token = null;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private string Resolve(SecretReference reference)
    {
        using var value = _secrets.Resolve(reference, Accessor);
        return value.Value;
    }

    private static string StatusValue(YemeksepetiOutboundStatus status) => status switch
    {
        YemeksepetiOutboundStatus.ReadyForPickup => "READY_FOR_PICKUP",
        YemeksepetiOutboundStatus.Dispatched => "DISPATCHED",
        YemeksepetiOutboundStatus.Cancelled => "CANCELLED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private static string ReasonValue(YemeksepetiCancellationReason reason) => reason switch
    {
        YemeksepetiCancellationReason.Closed => "CLOSED",
        YemeksepetiCancellationReason.ItemUnavailable => "ITEM_UNAVAILABLE",
        YemeksepetiCancellationReason.TooBusy => "TOO_BUSY",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
    };

    private sealed class PartnerClientSecretPolicy : ISecretAccessPolicy
    {
        public bool IsAllowed(string accessor, SecretReference reference) =>
            string.Equals(accessor, Accessor, StringComparison.Ordinal)
            && (reference == BaseUrl || reference == ChainId || reference == ClientId || reference == ClientSecret
                || reference == VendorId);
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record OrderUpdateBody(
        [property: JsonPropertyName("order_id")] string OrderId,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("items")] IReadOnlyList<OrderItemBody> Items,
        [property: JsonPropertyName("cancellation"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CancellationBody? Cancellation);

    private sealed record OrderItemBody(
        [property: JsonPropertyName("sku")] string Sku,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("pricing")] OrderItemPricing Pricing);

    private sealed record OrderItemPricing([property: JsonPropertyName("quantity")] decimal Quantity);

    private sealed record CancellationBody([property: JsonPropertyName("reason")] string Reason);

    private sealed record CatalogUpdateBody([property: JsonPropertyName("products")] IReadOnlyList<CatalogProductBody> Products);

    private sealed record CatalogProductBody(
        [property: JsonPropertyName("sku")] string Sku,
        [property: JsonPropertyName("price"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? Price,
        [property: JsonPropertyName("active"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Active,
        [property: JsonPropertyName("quantity"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? Quantity);
}
