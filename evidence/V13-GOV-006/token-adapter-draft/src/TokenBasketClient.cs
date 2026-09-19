using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace ALKAROS.Payments.Token.Draft;

/// <summary>
/// V13-GOV-006 DRAFT (unverified — no real Token credential/terminal was
/// ever reached; see README.md). Endpoint paths, header names, and every
/// request/response shape come from the real Postman collection Semih
/// exported from Token's own `postman.co` workspace, saved at
/// `evidence/v0/integrations/V0-HUG-001/tokenx-documentation.postman_collection.json`.
/// Style mirrors `ALKAROS.QrRelay.PublicGateway.CloudflareApiClient` — the
/// only other external-provider HTTP client in this codebase.
/// </summary>
public sealed class TokenBasketClient
{
    private readonly HttpClient _httpClient;
    private readonly string _authBaseUrl;
    private readonly string _basketBaseUrl;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly Func<DateTimeOffset> _clock;

    private string? _cachedAccessToken;
    private DateTimeOffset _cachedAccessTokenExpiresAt = DateTimeOffset.MinValue;

    public TokenBasketClient(
        HttpClient httpClient,
        string authBaseUrl,
        string basketBaseUrl,
        string clientId,
        string clientSecret,
        Func<DateTimeOffset>? clock = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentException.ThrowIfNullOrWhiteSpace(authBaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(basketBaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);
        _authBaseUrl = authBaseUrl.TrimEnd('/');
        _basketBaseUrl = basketBaseUrl.TrimEnd('/');
        _clientId = clientId;
        _clientSecret = clientSecret;
        // Injectable only so tests can fast-forward past the 86400s cache
        // window without a real sleep; production callers never pass this.
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// POST {authBaseUrl}/v1/auth/token, Basic Auth (client-id/client-secret).
    /// The real doc says the token is valid 86400s; a cached token is reused
    /// until 60s before that (never verified against a real clock skew).
    /// </summary>
    public async Task<string> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedAccessToken is not null && _clock() < _cachedAccessTokenExpiresAt)
            return _cachedAccessToken;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_authBaseUrl}/v1/auth/token");
        var basicAuthBytes = Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(basicAuthBytes));

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        // NOTE: unlike every other endpoint (success = body `status: 0`),
        // the saved Auth example returns body `status: 201` on success
        // ("User authenticated successfully") — Token is not internally
        // consistent about this, so Auth is checked on HTTP success + a
        // non-null `result.accessToken` only, not on a specific body status
        // value. This asymmetry is unverified against a real call.
        TokenAuthResult? authResult;
        try
        {
            authResult = await response.Content.ReadFromJsonAsync<TokenAuthResult>(cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new TokenApiException($"Token auth cevabı geçersiz JSON (HTTP {(int)response.StatusCode}).");
        }

        if (!response.IsSuccessStatusCode || authResult is null)
            throw new TokenApiException(
                $"Token auth başarısız (HTTP {(int)response.StatusCode}): {authResult?.Description}",
                authResult?.Status);

        var accessToken = authResult.Result?.AccessToken
            ?? throw new TokenApiException("Token auth cevabında accessToken yok.", authResult.Status);

        _cachedAccessToken = accessToken;
        _cachedAccessTokenExpiresAt = _clock().AddSeconds(86400 - 60);
        return accessToken;
    }

    /// <summary>
    /// POST {basketBaseUrl}/v1/instant-basket, header `terminal-id` only
    /// (never `branch-id` — confirmed by the Postman example headers).
    /// Returns nothing on success (201); errors throw with the real Token
    /// `status` code (1100 = terminal already has an open basket, 1104 =
    /// terminal not in instant mode).
    /// </summary>
    public async Task AddInstantBasketAsync(
        string terminalId,
        TokenAddInstantBasketRequest basketRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(terminalId);
        ArgumentNullException.ThrowIfNull(basketRequest);

        var accessToken = await AuthenticateAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_basketBaseUrl}/v1/instant-basket");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("terminal-id", terminalId);
        request.Content = JsonContent.Create(basketRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ReadEnvelopeAsync<object>(response, cancellationToken);
    }

    /// <summary>
    /// GET {basketBaseUrl}/v1/basket/{basketID}, header `terminal-id`.
    /// </summary>
    public async Task<TokenBasketDetails> GetBasketDetailsAsync(
        string terminalId,
        Guid basketId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(terminalId);

        var accessToken = await AuthenticateAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_basketBaseUrl}/v1/basket/{basketId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("terminal-id", terminalId);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var envelope = await ReadEnvelopeAsync<TokenBasketDetails>(response, cancellationToken);

        return envelope.Result
            ?? throw new TokenApiException("Token basket details cevabında result yok.", envelope.Status);
    }

    /// <summary>
    /// Polls `Get Basket Details` until `sale` is populated (the basket was
    /// paid/failed/voided) or <paramref name="maxAttempts"/> is exhausted —
    /// this is the "polling" reconciliation strategy chosen in
    /// `V13-GOV-005`/`docs/domain/token-integration-path-decision.md` over
    /// trusting an inbound webhook. Returns null on timeout — the caller
    /// (`TokenTenderHandler`) maps that to "requires reconciliation", never
    /// to an implicit approve/decline (CORR:C29, same invariant
    /// `TenderHandlerResult` already documents).
    /// </summary>
    public async Task<TokenSaleResult?> PollUntilSettledAsync(
        string terminalId,
        Guid basketId,
        int maxAttempts = 10,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        var interval = pollInterval ?? TimeSpan.FromSeconds(2);
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var details = await GetBasketDetailsAsync(terminalId, basketId, cancellationToken);
            if (details.Sale is not null)
                return details.Sale;

            await Task.Delay(interval, cancellationToken);
        }

        return null;
    }

    private static async Task<TokenApiEnvelope<TResult>> ReadEnvelopeAsync<TResult>(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        TokenApiEnvelope<TResult>? envelope;
        try
        {
            envelope = await response.Content.ReadFromJsonAsync<TokenApiEnvelope<TResult>>(cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new TokenApiException($"Token API'sinden geçersiz cevap alındı (HTTP {(int)response.StatusCode}).");
        }

        if (envelope is null)
            throw new TokenApiException($"Token API'sinden boş cevap alındı (HTTP {(int)response.StatusCode}).");

        // Token's own examples show `status: 0` for success and a non-zero
        // status (6, 403, 404, 1006, 1018, 1100, 1104, 1105, ...) alongside a
        // matching non-2xx HTTP code for every documented error — both
        // signals are checked since neither alone was proven sufficient
        // against a real terminal.
        if (!response.IsSuccessStatusCode || envelope.Status != 0)
            throw new TokenApiException(
                $"Token API hatası: {envelope.Description} (status={envelope.Status}, HTTP {(int)response.StatusCode}).",
                envelope.Status);

        return envelope;
    }
}
