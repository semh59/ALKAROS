using ALKAROS.Secrets;

namespace ALKAROS.Security.SecretRotation;

/// <summary>
/// Default <see cref="IRotatingSecretResolver"/>: tries the Active version
/// first, then each still-valid Overlap version (newest first) when the
/// provider has no value for a candidate — modelling provider outage /
/// misconfiguration recovery without ever masking an access-denied result.
/// </summary>
public sealed class RotatingSecretResolver : IRotatingSecretResolver
{
    private readonly ISecretRotationStore _store;
    private readonly ISecretResolver _resolver;

    public RotatingSecretResolver(ISecretRotationStore store, ISecretResolver resolver)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <inheritdoc/>
    public SecretValue Resolve(string secretName, string accessor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessor);

        var rotation = _store.Find(secretName)
            ?? throw new SecretRotationUnavailableException(secretName);

        var candidates = new List<int>();
        if (rotation.ActiveVersion is { } active)
            candidates.Add(active.Version);
        candidates.AddRange(
            rotation.OverlapVersions
                .OrderByDescending(v => v.ActivatedAtUtc)
                .Select(v => v.Version));

        foreach (var version in candidates)
        {
            var reference = SecretVersionReferenceNaming.ReferenceFor(secretName, version);
            try
            {
                return _resolver.Resolve(reference, accessor);
            }
            catch (SecretNotFoundException)
            {
                // The provider has no value for this candidate version yet
                // (outage/misconfiguration) - fall over to the next one
                // rather than failing the whole resolution.
            }
        }

        throw new SecretRotationUnavailableException(secretName);
    }
}
