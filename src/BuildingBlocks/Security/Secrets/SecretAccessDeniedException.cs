namespace ALKAROS.Secrets;

/// <summary>
/// Raised when an accessor is not permitted to read a secret by the
/// configured <see cref="ISecretAccessPolicy"/>. This failure is raised
/// before the provider is consulted so that a denied accessor learns
/// nothing about whether the secret exists.
/// </summary>
public sealed class SecretAccessDeniedException : SecretResolutionException
{
    public SecretAccessDeniedException(string secretName, string accessor)
        : base($"Accessor '{accessor}' is not allowed to read secret '{secretName}'.")
    {
    }
}
