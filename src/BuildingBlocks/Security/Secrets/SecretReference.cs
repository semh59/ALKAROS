namespace ALKAROS.Secrets;

/// <summary>
/// A typed, immutable address of a single secret. The reference carries the
/// secret name only — never the secret value. Secret values are resolved
/// through <see cref="ISecretResolver"/> and are deliberately not part of
/// this type so that references can safely appear in settings, logs and
/// exceptions.
/// </summary>
public sealed record SecretReference
{
    /// <summary>
    /// The secret name used to look the value up in a
    /// <see cref="ISecretProvider"/>. A name must be non-empty and must not
    /// contain whitespace.
    /// </summary>
    public string Name { get; }

    public SecretReference(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Any(char.IsWhiteSpace))
            throw new ArgumentException("Secret name must not contain whitespace.", nameof(name));
        Name = name;
    }

    /// <inheritdoc/>
    public override string ToString() => Name;
}
