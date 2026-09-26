using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ALKAROS.OnlineOrdering.Providers.Contracts;
using ALKAROS.Secrets;

namespace ALKAROS.OnlineOrdering.Providers.TrendyolGo.OrderIntake;

/// <summary>
/// V12-TGO-002: the platform settings every Trendyol Go call needs (entered on the platform settings screen or as
/// environment variables). UNVERIFIED DRAFT (EXT:TGO-MEAL-API "Authorization", "Get Order Packages"): basic
/// authentication with the API key and secret; every call carries <c>User-Agent: "{supplierId} - {integrator}"</c>,
/// <c>x-agentname</c> (the integrator) and <c>x-executor-user</c> (the e-mail of the person the call is made for).
/// </summary>
public sealed record TrendyolGoApiSettings(
    Uri BaseUrl, string SupplierId, string ApiKey, string ApiSecret, string IntegratorName, string ExecutorEmail)
{
    public static readonly SecretReference BaseUrlReference = new("trendyol-go-api-base-url");
    public static readonly SecretReference SupplierIdReference = new("trendyol-go-supplier-id");
    public static readonly SecretReference ApiKeyReference = new("trendyol-go-api-key");
    public static readonly SecretReference ApiSecretReference = new("trendyol-go-api-secret");
    public static readonly SecretReference IntegratorNameReference = new("trendyol-go-integrator-name");
    public static readonly SecretReference ExecutorEmailReference = new("trendyol-go-executor-email");

    public const string Accessor = "online-ordering.trendyol-go-api";

    /// <summary>All settings, or null when any is missing (the channel is off).</summary>
    public static TrendyolGoApiSettings? Resolve(ISecretProvider secrets)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        var resolver = new SecretResolver(secrets, new Policy());
        string? Read(SecretReference reference)
        {
            try
            {
                using var secret = resolver.Resolve(reference, Accessor);
                return secret.Value.Trim();
            }
            catch (SecretNotFoundException)
            {
                return null;
            }
        }

        var baseUrl = Read(BaseUrlReference);
        var supplierId = Read(SupplierIdReference);
        var apiKey = Read(ApiKeyReference);
        var apiSecret = Read(ApiSecretReference);
        var integrator = Read(IntegratorNameReference);
        var executor = Read(ExecutorEmailReference);
        if (baseUrl is null || supplierId is null || apiKey is null || apiSecret is null || integrator is null || executor is null
            || !Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return null;
        return new TrendyolGoApiSettings(uri, supplierId, apiKey, apiSecret, integrator, executor);
    }

    /// <summary>The documented headers of every Trendyol Go call.</summary>
    public void Apply(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ApiKey}:{ApiSecret}")));
        request.Headers.TryAddWithoutValidation("User-Agent", $"{SupplierId} - {IntegratorName}");
        request.Headers.TryAddWithoutValidation("x-agentname", IntegratorName);
        request.Headers.TryAddWithoutValidation("x-executor-user", ExecutorEmail);
    }

    private sealed class Policy : ISecretAccessPolicy
    {
        public bool IsAllowed(string accessor, SecretReference reference) =>
            string.Equals(accessor, Accessor, StringComparison.Ordinal)
            && reference.Name.StartsWith(TrendyolGoEvents.Provider + "-", StringComparison.Ordinal)
            && !string.Equals(reference.Name, TrendyolGoWebhookInbox.WebhookSecret.Name, StringComparison.Ordinal);
    }
}

public sealed class TrendyolGoApiException : Exception
{
    public TrendyolGoApiException(string message) : base(message) { }
}

/// <summary>
/// V12-TGO-002: reads Trendyol Go packages modified in a time window, so orders reach the restaurant even when the
/// platform has switched the webhook off. UNVERIFIED DRAFT (EXT:TGO-MEAL-API "Get Order Packages", read 2026-09-27):
/// <c>GET {base}/integrator/order/meal/suppliers/{supplierId}/packages</c> with
/// <c>packageModificationStartDate</c>/<c>packageModificationEndDate</c> (epoch milliseconds), <c>page</c> and
/// <c>size</c> (at most 50); the answer's <c>content</c> holds the packages and <c>totalPages</c> the page count. The
/// document does not say how the list is ordered, so the cursor moves only after every page of a window was read:
/// the window is at most <see cref="Window"/> long and the next one starts <see cref="Overlap"/> before this one
/// ended (a package seen twice is the same inbox row).
/// </summary>
public sealed class TrendyolGoOrderPollingSource : IOnlineOrderPollingSource
{
    public const int PageSize = 50;
    public const int MaxPages = 20;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan Overlap = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan FirstLookBack = TimeSpan.FromHours(1);

    private readonly HttpClient _http;
    private readonly ISecretProvider _secrets;
    private readonly TimeProvider _time;

    public TrendyolGoOrderPollingSource(HttpClient http, ISecretProvider secrets, TimeProvider time)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public string Provider => TrendyolGoEvents.Provider;

    /// <summary>Far below the documented 50 requests per 10 seconds per endpoint even at <see cref="MaxPages"/> pages.</summary>
    public TimeSpan Interval => TimeSpan.FromSeconds(30);

    public async Task<OnlineOrderPollPage> PollAsync(string? cursor, CancellationToken cancellationToken = default)
    {
        var settings = TrendyolGoApiSettings.Resolve(_secrets) ?? throw new OnlineOrderPollingNotConfiguredException(Provider);
        var now = _time.GetUtcNow().ToUnixTimeMilliseconds();
        var start = cursor is not null && long.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out var stored)
            ? stored
            : now - (long)FirstLookBack.TotalMilliseconds;
        var end = Math.Min(now, start + (long)Window.TotalMilliseconds);
        if (end < start)
            end = start;

        var events = new List<PolledOrderEvent>();
        for (var page = 0; ; page++)
        {
            if (page == MaxPages)
                throw new TrendyolGoApiException($"More than {MaxPages} pages of packages in one window.");
            var uri = new Uri(settings.BaseUrl, string.Create(CultureInfo.InvariantCulture,
                $"integrator/order/meal/suppliers/{Uri.EscapeDataString(settings.SupplierId)}/packages"
                + $"?packageModificationStartDate={start}&packageModificationEndDate={end}&page={page}&size={PageSize}"));
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            settings.Apply(request);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new OnlineOrderPollRateLimitedException(response.Headers.RetryAfter?.Delta);
            if (!response.IsSuccessStatusCode)
                throw new TrendyolGoApiException($"Trendyol Go package list failed with HTTP {(int)response.StatusCode}.");

            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var totalPages = ReadPage(body, events);
            if (page + 1 >= totalPages)
                break;
        }

        var next = Math.Max(start, end - (long)Overlap.TotalMilliseconds);
        return new OnlineOrderPollPage(events, next.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Adds the page's packages to <paramref name="events"/>; returns the page count the answer states.</summary>
    private static int ReadPage(byte[] body, List<PolledOrderEvent> events)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            throw new TrendyolGoApiException("The package list answer has no content array.");
        foreach (var package in content.EnumerateArray())
        {
            if (package.ValueKind != JsonValueKind.Object
                || TrendyolGoOrderNormalizer.Identifier(package, "id") is not { } id
                || TrendyolGoOrderNormalizer.Identifier(package, "packageStatus") is not { } status)
                throw new TrendyolGoApiException("A listed package has no id or packageStatus.");
            // A status the document does not list is kept under its own name, so it reaches a person as unknown.
            var eventType = TrendyolGoEvents.FromPackageStatus(status) ?? status;
            events.Add(new PolledOrderEvent(
                TrendyolGoEvents.EventKey(eventType, id), id, eventType,
                TrendyolGoOrderNormalizer.Identifier(package, "packageModificationDate"),
                Encoding.UTF8.GetBytes(package.GetRawText())));
        }

        return root.TryGetProperty("totalPages", out var total) && total.ValueKind == JsonValueKind.Number && total.TryGetInt32(out var pages)
            ? pages
            : 1;
    }
}
