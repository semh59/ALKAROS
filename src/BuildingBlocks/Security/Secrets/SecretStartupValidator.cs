namespace ALKAROS.Secrets;

/// <summary>
/// Startup gate for secret resolution: verifies that every required secret
/// can be resolved by the given accessor before the dependent integration
/// starts. The validator only proves availability — it never retains any
/// secret value beyond the check, so resolved values are disposed
/// immediately.
/// </summary>
public sealed class SecretStartupValidator
{
    private readonly ISecretResolver _resolver;

    public SecretStartupValidator(ISecretResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <summary>
    /// Resolves every required secret for <paramref name="accessor"/> and
    /// disposes each value immediately.
    /// </summary>
    /// <exception cref="SecretAccessDeniedException">
    /// A required secret is not accessible to <paramref name="accessor"/>.
    /// </exception>
    /// <exception cref="SecretNotFoundException">
    /// A required secret is not configured.
    /// </exception>
    public void Validate(IReadOnlyCollection<SecretReference> required, string accessor)
    {
        ArgumentNullException.ThrowIfNull(required);

        foreach (var reference in required)
        {
            using var value = _resolver.Resolve(reference, accessor);
        }
    }
}
