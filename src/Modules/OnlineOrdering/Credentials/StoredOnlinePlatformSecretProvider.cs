using ALKAROS.Secrets;

namespace ALKAROS.OnlineOrdering.Credentials;

/// <summary>
/// V12-OUI-003: a platform setting (<c>{provider}-{field}</c>, see <see cref="OnlinePlatformCredentialCatalog"/>)
/// comes from the settings a manager stored first and from <paramref name="fallback"/> (the environment
/// variables used so far) only when that field is not stored; every other secret, the envelope master key
/// included, comes from <paramref name="fallback"/> alone.
/// </summary>
public sealed class StoredOnlinePlatformSecretProvider(IOnlinePlatformCredentialStore store, ISecretProvider fallback) : ISecretProvider
{
    private readonly IOnlinePlatformCredentialStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ISecretProvider _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));

    public string? GetValue(SecretReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return OnlinePlatformCredentialCatalog.FromSecretReference(reference.Name) is { } setting
            ? _store.ResolveValue(setting.Provider, setting.Field) ?? _fallback.GetValue(reference)
            : _fallback.GetValue(reference);
    }
}
