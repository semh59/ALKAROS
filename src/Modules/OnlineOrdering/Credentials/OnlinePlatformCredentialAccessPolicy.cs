using ALKAROS.Secrets;
using ALKAROS.SensitiveData;

namespace ALKAROS.OnlineOrdering.Credentials;

/// <summary>
/// V12-OUI-003. Only the platform settings store may resolve the envelope master key and open its envelopes.
/// Like the webhook inbox's policy, it is deliberately not registered as the shared
/// <see cref="ISecretAccessPolicy"/>/<see cref="ISensitiveDataAccessPolicy"/> singleton; the store builds its own
/// resolver/cipher/protector chain around it.
/// </summary>
public sealed class OnlinePlatformCredentialAccessPolicy : ISecretAccessPolicy, ISensitiveDataAccessPolicy
{
    public const string Accessor = "online-ordering.platform-credential-store";

    public bool IsAllowed(string accessor, SecretReference reference) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);

    public bool CanRead(string accessor, SensitiveEnvelope envelope) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);
}
