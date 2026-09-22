namespace ALKAROS.Security.SecretRotation;

/// <summary>
/// Thrown when no usable version of a secret could be resolved: either the
/// secret has no rotation state at all, or every candidate version (the
/// Active one and every still-valid Overlap one) failed to resolve through
/// the underlying provider.
/// </summary>
public sealed class SecretRotationUnavailableException : Exception
{
    public string SecretName { get; }

    public SecretRotationUnavailableException(string secretName)
        : base($"No usable version of secret '{secretName}' could be resolved.")
    {
        SecretName = secretName;
    }
}
