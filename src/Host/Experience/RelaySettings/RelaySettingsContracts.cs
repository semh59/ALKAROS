namespace ALKAROS.Host.Experience.RelaySettings;

/// <summary>
/// V14-QRT-003. The raw token is accepted exactly once and never returned —
/// every read-back is <see cref="RelayCredentialStatusResponse"/> only.
/// </summary>
public sealed record SaveRelayCredentialRequest(string CloudflareApiToken);

public sealed record RelayCredentialStatusResponse(bool Configured, DateTimeOffset? UpdatedAt);
