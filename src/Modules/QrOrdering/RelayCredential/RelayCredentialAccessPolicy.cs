using ALKAROS.Secrets;
using ALKAROS.SensitiveData;

namespace ALKAROS.QrOrdering.RelayCredential;

/// <summary>
/// V12-QRT-003: the only component allowed to resolve the envelope master
/// key or decrypt the stored relay credential is
/// <see cref="PostgresRelayCredentialStore"/> itself, identified by
/// <see cref="Accessor"/> — a second line of defense behind the HTTP-level
/// `integrations.manage` permission check: even other backend code could
/// not read the decrypted token without also being this exact accessor.
/// </summary>
public sealed class RelayCredentialAccessPolicy : ISecretAccessPolicy, ISensitiveDataAccessPolicy
{
    public const string Accessor = "qr-ordering.relay-credential-store";

    public bool IsAllowed(string accessor, SecretReference reference) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);

    public bool CanRead(string accessor, SensitiveEnvelope envelope) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);
}
