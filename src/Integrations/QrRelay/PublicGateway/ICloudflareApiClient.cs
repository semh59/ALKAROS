namespace ALKAROS.QrRelay.PublicGateway;

public sealed record CloudflareTunnel(string Id, string Name);

/// <summary>Thin wrapper over exactly the three Cloudflare REST API operations relay onboarding needs.</summary>
public interface ICloudflareApiClient
{
    Task<CloudflareTunnel> CreateTunnelAsync(string apiToken, string accountId, string name, CancellationToken cancellationToken = default);

    /// <summary>The opaque run token `cloudflared tunnel run --token &lt;value&gt;` accepts directly — no separate credentials.json.</summary>
    Task<string> GetTunnelTokenAsync(string apiToken, string accountId, string tunnelId, CancellationToken cancellationToken = default);

    Task CreateDnsRecordAsync(string apiToken, string zoneId, string subdomainLabel, string target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells Cloudflare's edge where to route requests for <paramref name="hostname"/>
    /// once they arrive over the tunnel: to <paramref name="originService"/>
    /// (e.g. <c>http://api:5080</c> — the LocalConnector's own container
    /// runs `cloudflared` as an independent container on the compose network,
    /// V12-QRT-005, reaching the API by its compose service DNS name).
    /// Without this, a tunnel can be healthy and connected yet still
    /// answer every request with cloudflared's own 404 — a connected tunnel is
    /// not the same as a configured one.
    /// </summary>
    Task SetTunnelConfigurationAsync(string apiToken, string accountId, string tunnelId, string hostname, string originService, CancellationToken cancellationToken = default);

    /// <summary>Cloudflare refuses this while the tunnel has an active connection — the caller must stop the local connector first.</summary>
    Task DeleteTunnelAsync(string apiToken, string accountId, string tunnelId, CancellationToken cancellationToken = default);
}

/// <summary>A Cloudflare API call returned `success: false`, or the response could not be parsed. Carries Cloudflare's own error message, never fabricated.</summary>
public sealed class CloudflareApiException : Exception
{
    public CloudflareApiException(string message) : base(message) { }
}
