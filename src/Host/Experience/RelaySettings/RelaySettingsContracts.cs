namespace ALKAROS.Host.Experience.RelaySettings;

/// <summary>
/// V14-QRT-003 (token, never returned) + V14-QRT-001 (AccountId/ZoneId/
/// BaseDomain: not secret — Cloudflare treats them as public identifiers —
/// so they round-trip back through <see cref="RelayCredentialStatusResponse"/>,
/// unlike the token).
/// </summary>
public sealed record SaveRelayCredentialRequest(string CloudflareApiToken, string AccountId, string ZoneId, string BaseDomain);

public sealed record RelayCredentialStatusResponse(
    bool Configured,
    DateTimeOffset? UpdatedAt,
    string? AccountId,
    string? ZoneId,
    string? BaseDomain);
