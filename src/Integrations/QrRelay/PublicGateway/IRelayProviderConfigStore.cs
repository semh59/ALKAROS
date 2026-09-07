namespace ALKAROS.QrRelay.PublicGateway;

/// <summary>
/// Non-secret Cloudflare configuration a tunnel/DNS API call needs
/// alongside the credential V12-QRT-003 already stores encrypted
/// (account/zone ids are not sensitive — Cloudflare treats them as public
/// identifiers, unlike the API token itself).
/// </summary>
public sealed record RelayProviderConfig(string AccountId, string ZoneId, string BaseDomain, DateTimeOffset UpdatedAt);

public interface IRelayProviderConfigStore
{
    Task SaveAsync(string accountId, string zoneId, string baseDomain, CancellationToken cancellationToken = default);

    Task<RelayProviderConfig?> GetAsync(CancellationToken cancellationToken = default);
}
