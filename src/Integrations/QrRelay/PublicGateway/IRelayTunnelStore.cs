namespace ALKAROS.QrRelay.PublicGateway;

/// <summary>Non-secret facts about the provisioned tunnel — safe to return from an HTTP status endpoint.</summary>
public sealed record RelayTunnelInfo(string TunnelId, string Hostname, DateTimeOffset UpdatedAt);

/// <summary>
/// V12-QRT-001. The Cloudflare Tunnel run-token
/// (<see cref="ICloudflareApiClient.GetTunnelTokenAsync"/>'s result) is
/// itself a bearer credential — anyone holding it can run the tunnel as this
/// restaurant — so it is stored the same way the API token is
/// (<c>ALKAROS.QrOrdering.RelayCredential.PostgresRelayCredentialStore</c>,
/// same master key and accessor identity): as an AES-256-GCM envelope,
/// never plaintext, never returned by an HTTP endpoint.
/// </summary>
public interface IRelayTunnelStore
{
    Task SaveAsync(string tunnelId, string tunnelToken, string hostname, CancellationToken cancellationToken = default);

    /// <summary>Whether a tunnel has been provisioned, and its non-secret identifying facts.</summary>
    Task<RelayTunnelInfo?> GetInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts and returns the stored run-token for the <c>cloudflared</c>
    /// process the local connector starts. Never exposed through an HTTP
    /// endpoint — callers are backend automation only.
    /// </summary>
    Task<string?> ResolveTunnelTokenAsync(CancellationToken cancellationToken = default);
}
