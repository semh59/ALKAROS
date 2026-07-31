namespace ALKAROS.Secrets;

/// <summary>
/// In-memory <see cref="ISecretProvider"/> used for tests and development
/// environments. Values are seeded explicitly through <see cref="Set"/> and
/// live only for the lifetime of the provider instance — they are never
/// persisted, written to disk or exposed through settings.
/// </summary>
public sealed class InMemorySecretProvider : ISecretProvider
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <summary>
    /// Seeds a secret value. Empty or whitespace-only values are rejected so
    /// that a configured secret always carries a usable credential.
    /// </summary>
    public void Set(SecretReference reference, string value)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        _values[reference.Name] = value;
    }

    /// <inheritdoc/>
    public string? GetValue(SecretReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return _values.TryGetValue(reference.Name, out var value) ? value : null;
    }
}
