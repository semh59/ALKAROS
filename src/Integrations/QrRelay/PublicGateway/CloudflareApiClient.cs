using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ALKAROS.QrRelay.PublicGateway;

/// <summary>
/// V14-QRT-001. Every request/response shape here was verified against
/// Cloudflare's own current API reference (developers.cloudflare.com/api,
/// the cfd_tunnel and dns_records resources), not guessed:
/// - POST /accounts/{account_id}/cfd_tunnel — name, config_src ("cloudflare"
///   or "local"); tunnel_secret is optional, omitted here (Cloudflare
///   generates one — the token-based run mode below never needs it).
/// - GET /accounts/{account_id}/cfd_tunnel/{tunnel_id}/token — the bare
///   opaque token `cloudflared tunnel run --token` accepts directly.
/// - POST /zones/{zone_id}/dns_records — type/name/content required;
///   proxied: false is required here (V0-ARC-009: Cloudflare terminates
///   public TLS, but must not additionally proxy/cache tunnel traffic).
/// - DELETE /accounts/{account_id}/cfd_tunnel/{tunnel_id} — Cloudflare
///   itself refuses this while the tunnel has an active connection.
/// </summary>
public sealed class CloudflareApiClient : ICloudflareApiClient
{
    private const string BaseUrl = "https://api.cloudflare.com/client/v4";

    private readonly HttpClient _httpClient;

    public CloudflareApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<CloudflareTunnel> CreateTunnelAsync(string apiToken, string accountId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var result = await SendAsync<CreateTunnelResult>(
            apiToken, HttpMethod.Post, $"/accounts/{accountId}/cfd_tunnel",
            new { name, config_src = "cloudflare" }, cancellationToken);

        return new CloudflareTunnel(result.Id, result.Name);
    }

    public Task<string> GetTunnelTokenAsync(string apiToken, string accountId, string tunnelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tunnelId);

        return SendAsync<string>(apiToken, HttpMethod.Get, $"/accounts/{accountId}/cfd_tunnel/{tunnelId}/token", body: null, cancellationToken);
    }

    public Task CreateDnsRecordAsync(string apiToken, string zoneId, string subdomainLabel, string target, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(subdomainLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        return SendAsync<DnsRecordResult>(
            apiToken, HttpMethod.Post, $"/zones/{zoneId}/dns_records",
            new { type = "CNAME", name = subdomainLabel, content = target, ttl = 3600, proxied = false },
            cancellationToken);
    }

    public Task DeleteTunnelAsync(string apiToken, string accountId, string tunnelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tunnelId);

        return SendAsync<DeleteTunnelResult>(
            apiToken, HttpMethod.Delete, $"/accounts/{accountId}/cfd_tunnel/{tunnelId}", body: null, cancellationToken);
    }

    private async Task<T> SendAsync<T>(string apiToken, HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiToken);

        using var request = new HttpRequestMessage(method, BaseUrl + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        CloudflareEnvelope<T>? envelope;
        try
        {
            envelope = await response.Content.ReadFromJsonAsync<CloudflareEnvelope<T>>(cancellationToken: cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new CloudflareApiException("Cloudflare API'sinden geçersiz cevap alındı.");
        }

        if (envelope is null)
            throw new CloudflareApiException("Cloudflare API'sinden boş cevap alındı.");

        if (!envelope.Success)
        {
            var message = envelope.Errors.Count > 0 ? envelope.Errors[0].Message : $"HTTP {(int)response.StatusCode}.";
            throw new CloudflareApiException($"Cloudflare API hatası: {message}");
        }

        return envelope.Result is null
            ? throw new CloudflareApiException("Cloudflare API başarı bildirdi ama sonuç göndermedi.")
            : envelope.Result;
    }

    private sealed record CloudflareEnvelope<T>(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("result")] T? Result,
        [property: JsonPropertyName("errors")] IReadOnlyList<CloudflareError> Errors);

    private sealed record CloudflareError([property: JsonPropertyName("message")] string Message);

    private sealed record CreateTunnelResult(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name);

    private sealed record DnsRecordResult([property: JsonPropertyName("id")] string Id);

    private sealed record DeleteTunnelResult([property: JsonPropertyName("id")] string Id);
}
