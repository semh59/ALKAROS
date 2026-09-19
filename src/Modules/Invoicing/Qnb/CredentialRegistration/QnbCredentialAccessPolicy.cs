using ALKAROS.Secrets;
using ALKAROS.SensitiveData;

namespace ALKAROS.Invoicing.Qnb.CredentialRegistration;

/// <summary>
/// V14-QNB-006. Mirrors `ALKAROS.Payments.Token.TerminalCredential.
/// TokenTerminalCredentialAccessPolicy`'s exact shape and rationale:
/// deliberately NOT registered as the shared `ISecretAccessPolicy`/
/// `ISensitiveDataAccessPolicy` singletons (see that type's own doc
/// comment for why a third module registering either interface globally
/// would silently collide with the other two). `PostgresQnbCredentialStore`
/// builds its own private resolver/cipher/protector chain around this
/// policy instead.
/// </summary>
public sealed class QnbCredentialAccessPolicy : ISecretAccessPolicy, ISensitiveDataAccessPolicy
{
    public const string Accessor = "invoicing.qnb-credential-store";

    public bool IsAllowed(string accessor, SecretReference reference) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);

    public bool CanRead(string accessor, SensitiveEnvelope envelope) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);
}
