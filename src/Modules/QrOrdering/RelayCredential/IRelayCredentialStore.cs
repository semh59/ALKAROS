namespace ALKAROS.QrOrdering.RelayCredential;

public sealed record RelayCredentialStatus(bool Configured, DateTimeOffset? UpdatedAt);

public interface IRelayCredentialStore
{
    /// <summary>Encrypts and stores the relay provider's API token, replacing whatever was stored before.</summary>
    Task SaveCloudflareApiTokenAsync(string rawToken, Guid? updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Whether a credential is configured, and when it was last set — never the value itself.</summary>
    Task<RelayCredentialStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts and returns the stored raw token for the automation that
    /// actually calls the relay provider's API. Returns <c>null</c> when
    /// nothing is configured yet. Never exposed through an HTTP endpoint —
    /// callers are backend automation only.
    /// </summary>
    Task<string?> ResolveCloudflareApiTokenAsync(CancellationToken cancellationToken = default);
}
