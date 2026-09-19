using ALKAROS.Secrets;
using ALKAROS.SensitiveData;

namespace ALKAROS.Payments.Token.TerminalCredential;

/// <summary>
/// V13-HUG-005. Mirrors `ALKAROS.QrOrdering.RelayCredential.
/// RelayCredentialAccessPolicy`'s exact shape — the only component allowed
/// to resolve the envelope master key or decrypt the stored Token
/// credential is <see cref="PostgresTokenTerminalCredentialStore"/> itself.
///
/// Deliberately NOT registered as the shared `ISecretAccessPolicy`/
/// `ISensitiveDataAccessPolicy` singletons (`QrOrderingModule` already
/// registers `RelayCredentialAccessPolicy` there, and both interfaces
/// resolve to a SINGLE instance per Host — a second global registration
/// would silently replace or be replaced by the other module's policy,
/// breaking whichever one lost, since each only recognizes its own
/// hardcoded accessor string). Instead, `PostgresTokenTerminalCredentialStore`
/// constructs its own private `SecretResolver`/`AesGcmEnvelopeCipher`/
/// `SensitivePayloadProtector` chain around this policy, reusing only the
/// stateless, no-accessor-affinity `ISecretProvider` from DI.
/// </summary>
public sealed class TokenTerminalCredentialAccessPolicy : ISecretAccessPolicy, ISensitiveDataAccessPolicy
{
    public const string Accessor = "payments.token-terminal-credential-store";

    public bool IsAllowed(string accessor, SecretReference reference) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);

    public bool CanRead(string accessor, SensitiveEnvelope envelope) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);
}
