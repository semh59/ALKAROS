namespace ALKAROS.Secrets;

/// <summary>
/// Decides whether an accessor may read a secret. Accessors are the
/// identifiers of the components requesting a secret (for example an
/// integration module name). The policy is the least-privilege boundary:
/// the default of the resolution flow is to deny, and an explicit
/// <see cref="IsAllowed"/> result is required for every read.
/// </summary>
public interface ISecretAccessPolicy
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="accessor"/> is permitted to
    /// read the secret identified by <paramref name="reference"/>.
    /// </summary>
    bool IsAllowed(string accessor, SecretReference reference);
}
