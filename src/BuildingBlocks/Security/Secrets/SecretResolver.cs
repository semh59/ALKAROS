namespace ALKAROS.Secrets;

/// <summary>
/// Default <see cref="ISecretResolver"/> implementation. Access is checked
/// before the provider is consulted (fail-closed: a denied accessor learns
/// nothing about secret existence), and a missing secret produces a typed
/// <see cref="SecretNotFoundException"/>.
/// </summary>
public sealed class SecretResolver : ISecretResolver
{
    private readonly ISecretProvider _provider;
    private readonly ISecretAccessPolicy _policy;

    public SecretResolver(ISecretProvider provider, ISecretAccessPolicy policy)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    /// <inheritdoc/>
    public SecretValue Resolve(SecretReference reference, string accessor)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessor);

        if (!_policy.IsAllowed(accessor, reference))
            throw new SecretAccessDeniedException(reference.Name, accessor);

        var value = _provider.GetValue(reference);
        if (value is null)
            throw new SecretNotFoundException(reference.Name);

        return new SecretValue(value);
    }
}
