namespace ALKAROS.Invoicing.Qnb.CredentialRegistration;

/// <summary>
/// V14-QNB-006. `userId`/`vergiTcKimlikNo` are not secret (they identify
/// WHICH tenant, not a credential) so they come back plaintext; mirrors
/// `ALKAROS.Payments.Token.TerminalCredential.TokenTerminalCredentialStatus`'s
/// own shape. The `password` itself is never part of this record.
/// </summary>
public sealed record QnbCredentialStatus(
    bool Configured,
    DateTimeOffset? UpdatedAt,
    string? UserId,
    string? VergiTcKimlikNo);

public sealed record SaveQnbCredentialRequest(
    string UserId,
    string Password,
    string VergiTcKimlikNo);

/// <summary>
/// V14-QNB-006. Stores the values `V14-QNB-001/002` need to call QNB
/// eSolutions' SOAP API (`userId`/`password` for `wsLogin`,
/// `vergiTcKimlikNo` for `belgeGonderExt`/status queries — see
/// `evidence/v0/integrations/V0-QNB-001/**`). Does NOT call QNB's live
/// API itself; this is local configuration storage only.
/// </summary>
public interface IQnbCredentialStore
{
    /// <summary>Encrypts <paramref name="request"/>'s Password and stores everything, replacing whatever was stored before.</summary>
    Task SaveAsync(SaveQnbCredentialRequest request, Guid? updatedBy, CancellationToken cancellationToken = default);

    /// <summary>Everything except the password — safe to return over HTTP.</summary>
    Task<QnbCredentialStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts and returns the stored password, for backend automation
    /// that actually calls QNB's SOAP API (`V14-QNB-001/002`). Returns
    /// <c>null</c> when nothing is configured yet. Never exposed through
    /// an HTTP endpoint.
    /// </summary>
    Task<string?> ResolvePasswordAsync(CancellationToken cancellationToken = default);
}
