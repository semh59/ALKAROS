using ALKAROS.QrOrdering.RelayCredential;

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
    /// Chains CreateTunnel -&gt; GetTunnelToken -&gt; CreateDnsRecord using the
    /// already-saved API token and account/zone/base-domain config, then
    /// persists the resulting tunnel id/token/hostname. Idempotent in the
    /// sense that calling it again creates a brand new Cloudflare tunnel and
    /// replaces the stored one — Cloudflare itself refuses to delete a
    /// tunnel with an active connection, but nothing here manages that
    /// lifecycle; that is the (not yet built) LocalConnector's job.
    /// </summary>
    Task<RelayProvisioningResult> ProvisionAsync(string subdomainLabel, CancellationToken cancellationToken = default);
}

public sealed class RelayProvisioningService : IRelayProvisioningService
{
    private readonly ICloudflareApiClient _client;
    private readonly IRelayCredentialStore _credentialStore;
    private readonly IRelayProviderConfigStore _configStore;
    private readonly IRelayTunnelStore _tunnelStore;

    public RelayProvisioningService(
        ICloudflareApiClient client,
        IRelayCredentialStore credentialStore,
        IRelayProviderConfigStore configStore,
        IRelayTunnelStore tunnelStore)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        _tunnelStore = tunnelStore ?? throw new ArgumentNullException(nameof(tunnelStore));
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
        }
        catch (CloudflareApiException exception)
        {
            throw new RelayProvisioningException($"Cloudflare tünel kurulumu başarısız oldu: {exception.Message}");
        }

        await _tunnelStore.SaveAsync(tunnel.Id, tunnelToken, hostname, cancellationToken);
        return new RelayProvisioningResult(hostname);
    }
}
