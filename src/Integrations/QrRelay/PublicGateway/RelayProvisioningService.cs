using ALKAROS.QrOrdering.RelayCredential;
using Microsoft.Extensions.Logging;

namespace ALKAROS.QrRelay.PublicGateway;

public sealed record RelayProvisioningResult(string Hostname);

/// <summary>A provisioning step could not complete — the message is always safe to show a manager (never a raw Cloudflare error).</summary>
public sealed class RelayProvisioningException : Exception
{
    public RelayProvisioningException(string message) : base(message) { }
}

public interface IRelayProvisioningService
{
    /// <summary>
    /// Chains CreateTunnel -&gt; GetTunnelToken -&gt; CreateDnsRecord -&gt;
    /// SetTunnelConfiguration using the already-saved API token and
    /// account/zone/base-domain config, then persists the resulting tunnel
    /// id/token/hostname. Idempotent in the sense that calling it again
    /// creates a brand new Cloudflare tunnel and replaces the stored one —
    /// Cloudflare itself refuses to delete a tunnel with an active
    /// connection, but nothing here manages that lifecycle or cleans up the
    /// orphaned previous tunnel.
    /// </summary>
    Task<RelayProvisioningResult> ProvisionAsync(string subdomainLabel, CancellationToken cancellationToken = default);
}

public sealed class RelayProvisioningService : IRelayProvisioningService
{
    private static readonly Action<ILogger, string, Exception?> LogProvisioningFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(5510, nameof(LogProvisioningFailed)),
            "Cloudflare tunnel provisioning failed for subdomain '{SubdomainLabel}'.");


    /// <summary>
    /// The LocalConnector's own container runs `cloudflared` (see
    /// `deploy/docker/Dockerfile`'s `connector` stage and
    /// `ALKAROS.QrRelay.LocalConnector.CloudflaredProcessFactory`), on the
    /// compose network as its own independent container (V12-QRT-005) — so
    /// this targets the `api` service's compose DNS name, not "localhost".
    /// Revised from a shared network namespace (`network_mode:
    /// "service:api"`) specifically because that coupling meant restarting
    /// `api` also forced the connector to restart — this is the fix for
    /// that (see docs/architecture/qr-relay-topology.md's amendment note,
    /// V12-QRT-005: still no public inbound port, still the same physical
    /// host, still API-key authenticated — only the internal reachability
    /// mechanism changed).
    /// </summary>
    private const string LocalOriginService = "http://api:5080";

    private readonly ICloudflareApiClient _client;
    private readonly IRelayCredentialStore _credentialStore;
    private readonly IRelayProviderConfigStore _configStore;
    private readonly IRelayTunnelStore _tunnelStore;
    private readonly ILogger<RelayProvisioningService> _logger;

    public RelayProvisioningService(
        ICloudflareApiClient client,
        IRelayCredentialStore credentialStore,
        IRelayProviderConfigStore configStore,
        IRelayTunnelStore tunnelStore,
        ILogger<RelayProvisioningService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        _tunnelStore = tunnelStore ?? throw new ArgumentNullException(nameof(tunnelStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RelayProvisioningResult> ProvisionAsync(string subdomainLabel, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subdomainLabel);

        var apiToken = await _credentialStore.ResolveCloudflareApiTokenAsync(cancellationToken);
        if (apiToken is null)
            throw new RelayProvisioningException("Önce Cloudflare bağlantı anahtarı kaydedilmelidir.");

        var config = await _configStore.GetAsync(cancellationToken);
        if (config is null)
            throw new RelayProvisioningException("Önce hesap kimliği, bölge kimliği ve ana alan adı kaydedilmelidir.");

        CloudflareTunnel tunnel;
        string tunnelToken;
        var hostname = $"{subdomainLabel}.{config.BaseDomain}";
        try
        {
            tunnel = await _client.CreateTunnelAsync(apiToken, config.AccountId, $"alkaros-{subdomainLabel}", cancellationToken);
            tunnelToken = await _client.GetTunnelTokenAsync(apiToken, config.AccountId, tunnel.Id, cancellationToken);
            await _client.CreateDnsRecordAsync(apiToken, config.ZoneId, hostname, $"{tunnel.Id}.cfargotunnel.com", cancellationToken);
            await _client.SetTunnelConfigurationAsync(apiToken, config.AccountId, tunnel.Id, hostname, LocalOriginService, cancellationToken);
        }
        catch (CloudflareApiException exception)
        {
            // V1-RMD-138: found by an independent audit (2026-09-09) — this
            // class's own doc comment above promises the message is "always
            // safe to show a manager (never a raw Cloudflare error)", but
            // this line interpolated exception.Message — Cloudflare's own,
            // usually-English API error text — directly into it, breaking
            // that promise. The raw detail is still available, just logged
            // server-side instead of shown in the response
            // (docs/UI_STYLE_GUIDE.md: no raw external error text on screen).
            LogProvisioningFailed(_logger, subdomainLabel, exception);
            throw new RelayProvisioningException(
                "Cloudflare tünel kurulumu başarısız oldu. Cloudflare hesap ayarlarınızı (API anahtarı, hesap/bölge kimliği) kontrol edip tekrar deneyin.");
        }

        await _tunnelStore.SaveAsync(tunnel.Id, tunnelToken, hostname, cancellationToken);
        return new RelayProvisioningResult(hostname);
    }
}
