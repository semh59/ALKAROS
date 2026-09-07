namespace ALKAROS.Host.Experience.RelaySettings;

/// <summary>
/// V12-QRT-003 (token, never returned) + V12-QRT-001 (AccountId/ZoneId/
/// BaseDomain: not secret — Cloudflare treats them as public identifiers —
/// so they round-trip back through <see cref="RelayCredentialStatusResponse"/>,
/// unlike the token).
/// </summary>
public sealed record SaveRelayCredentialRequest(string CloudflareApiToken, string AccountId, string ZoneId, string BaseDomain);

/// <summary>
/// V12-QRT-001: TunnelHostname/TunnelUpdatedAt are non-secret facts about an
/// already-provisioned tunnel (null until the "enable connection" action
/// succeeds at least once) — never the tunnel run-token itself.
/// </summary>
/// <summary>
/// ConnectorState is one of <see cref="ALKAROS.QrRelay.LocalConnector.RelayConnectorState"/>'s
/// names ("NotConfigured"/"Running"/"Restarting") — the client maps it to
/// Turkish text, per `docs/UI_STYLE_GUIDE.md` (a raw enum name is never
/// shown to a user directly).
/// </summary>
public sealed record RelayCredentialStatusResponse(
    bool Configured,
    DateTimeOffset? UpdatedAt,
    string? AccountId,
    string? ZoneId,
    string? BaseDomain,
    string? TunnelHostname,
    DateTimeOffset? TunnelUpdatedAt,
    string ConnectorState);

/// <summary>V12-QRT-001: a short, URL-safe label identifying this restaurant's subdomain (e.g. "sube1" -&gt; sube1.&lt;BaseDomain&gt;).</summary>
public sealed record ProvisionRelayTunnelRequest(string SubdomainLabel);

public sealed record ProvisionRelayTunnelResponse(string Hostname);
