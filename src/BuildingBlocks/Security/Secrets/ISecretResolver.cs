namespace ALKAROS.Secrets;

/// <summary>
/// The secret resolution boundary: combines an
/// <see cref="ISecretAccessPolicy"/> and an <see cref="ISecretProvider"/> so
/// that a secret value is handed out only to an authorized accessor.
/// Resolved values are short-lived and must be disposed by the caller.
/// </summary>
public interface ISecretResolver
{
    /// <summary>
    /// Resolves <paramref name="reference"/> on behalf of
    /// <paramref name="accessor"/>.
    /// </summary>
    /// <exception cref="SecretAccessDeniedException">
    /// The accessor is not permitted to read the secret. Raised before the
    /// provider is consulted.
    /// </exception>
    /// <exception cref="SecretNotFoundException">
    /// The secret is not configured in the provider.
    /// </exception>
    SecretValue Resolve(SecretReference reference, string accessor);
}
