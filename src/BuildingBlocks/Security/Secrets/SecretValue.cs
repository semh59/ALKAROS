namespace ALKAROS.Secrets;

/// <summary>
/// The resolved value of a secret, guarded by a short-lived boundary: the
/// value is readable only until <see cref="Dispose"/> is called and never
/// surfaces through <see cref="ToString"/>, string formatting or exception
/// formatting. Callers must dispose the value as soon as they are done with
/// it so that the credential does not outlive its intended use.
/// </summary>
public sealed class SecretValue : IDisposable
{
    private string? _value;

    internal SecretValue(string value) => _value = value;

    /// <summary>
    /// The raw secret value. Access after <see cref="Dispose"/> throws
    /// <see cref="ObjectDisposedException"/>.
    /// </summary>
    public string Value =>
        _value ?? throw new ObjectDisposedException(nameof(SecretValue));

    /// <summary>
    /// Always returns a fixed redacted representation; the secret value is
    /// never exposed through string conversion.
    /// </summary>
    public override string ToString() => "<secret>";

    /// <summary>
    /// Releases the secret value so that further reads fail closed.
    /// </summary>
    public void Dispose() => _value = null;
}
