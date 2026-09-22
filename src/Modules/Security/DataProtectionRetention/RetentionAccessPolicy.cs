using ALKAROS.Secrets;
using ALKAROS.SensitiveData;

namespace ALKAROS.Security.DataProtectionRetention;

/// <summary>
/// The only accessor allowed to resolve a retention subject's envelope key
/// or decrypt its envelope is <see cref="AuthorizedReEncryptionService"/>
/// itself, identified by <see cref="Accessor"/> — the same
/// private-per-consumer pattern as
/// <c>ALKAROS.QrOrdering.RelayCredential.RelayCredentialAccessPolicy</c>:
/// <c>ISensitiveDataAccessPolicy</c> has no shared DI registration by
/// design, so this policy is built privately inside
/// <see cref="AuthorizedReEncryptionService"/>'s own constructor rather than
/// resolved from the container.
/// </summary>
public sealed class RetentionAccessPolicy : ISecretAccessPolicy, ISensitiveDataAccessPolicy
{
    public const string Accessor = "security.data-protection-retention";

    public bool IsAllowed(string accessor, SecretReference reference) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);

    public bool CanRead(string accessor, SensitiveEnvelope envelope) =>
        string.Equals(accessor, Accessor, StringComparison.Ordinal);
}
