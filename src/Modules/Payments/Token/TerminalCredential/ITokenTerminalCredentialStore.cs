namespace ALKAROS.Payments.Token.TerminalCredential;

/// <summary>
/// V13-HUG-005. `merchantId`/`branchId`/`terminalId`/`clientId` are not
/// secret (they identify WHICH terminal, not a credential) so they come
/// back plaintext; <c>Configured</c>/<c>UpdatedAt</c> mirror
/// `ALKAROS.QrOrdering.RelayCredential.RelayCredentialStatus`'s own shape.
/// The `client-secret` itself is never part of this record.
/// </summary>
public sealed record TokenTerminalCredentialStatus(
    bool Configured,
    DateTimeOffset? UpdatedAt,
    string? MerchantId,
    string? BranchId,
    string? TerminalId,
    string? ClientId);

public sealed record SaveTokenTerminalCredentialRequest(
    string MerchantId,
    string BranchId,
    string TerminalId,
    string ClientId,
    string ClientSecret);

/// <summary>
/// V13-HUG-005. Stores the values `V13-HUG-001..004`/`V13-FSC-004` need to
/// call TokenX Connect Cloud (`merchant-id`/`branch-id`/`terminal-id`/
/// `client-id`/`client-secret`, found printed on the physical Token fiscal device or
/// its app's QR code — see the task file's Goal). Does NOT call Token's
/// live API itself; this is local configuration storage only.
/// </summary>
public interface ITokenTerminalCredentialStore
{
    /// <summary>Encrypts <paramref name="request"/>'s ClientSecret and stores everything, replacing whatever was stored before.</summary>
    Task SaveAsync(SaveTokenTerminalCredentialRequest request, Guid? updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Everything except the client secret — safe to return over HTTP.</summary>
    Task<TokenTerminalCredentialStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts and returns the stored client secret, for backend
    /// automation that actually calls TokenX Connect Cloud
    /// (`V13-HUG-001..004`). Returns <c>null</c> when nothing is
    /// configured yet. Never exposed through an HTTP endpoint.
    /// </summary>
    Task<string?> ResolveClientSecretAsync(CancellationToken cancellationToken = default);
}
