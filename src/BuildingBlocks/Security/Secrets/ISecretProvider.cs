namespace ALKAROS.Secrets;

/// <summary>
/// Supplies raw secret values by name. Implementations back onto a
/// protected store such as an OS credential vault or environment variables;
/// secrets must never be stored in source, settings or a database. A
/// missing secret is reported by returning <c>null</c>.
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// Returns the secret value for <paramref name="reference"/>, or
    /// <c>null</c> when the secret is not configured.
    /// </summary>
    string? GetValue(SecretReference reference);
}
