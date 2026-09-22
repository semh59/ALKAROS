using ALKAROS.Secrets;

namespace ALKAROS.Security.SecretRotation;

/// <summary>
/// Resolves a secret by its logical (unversioned) name, using the current
/// rotation state to pick the right underlying version — falling over to
/// the newest still-valid Overlap version when the Active version's
/// provider lookup fails.
/// </summary>
public interface IRotatingSecretResolver
{
    /// <exception cref="SecretAccessDeniedException">
    /// The accessor is not permitted to read the secret. Never retried
    /// against another version — a policy denial is not an outage.
    /// </exception>
    /// <exception cref="SecretRotationUnavailableException">
    /// The secret has no rotation state, or every candidate version failed
    /// to resolve through the underlying provider.
    /// </exception>
    SecretValue Resolve(string secretName, string accessor);
}
