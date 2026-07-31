namespace ALKAROS.Secrets;

/// <summary>
/// Raised when a referenced secret does not exist in the configured
/// <see cref="ISecretProvider"/>. The message contains the secret name only,
/// never the value.
/// </summary>
public sealed class SecretNotFoundException : SecretResolutionException
{
    public SecretNotFoundException(string secretName)
        : base($"Secret '{secretName}' is not configured.")
    {
    }
}
