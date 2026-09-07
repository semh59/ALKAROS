namespace ALKAROS.Secrets;

/// <summary>
/// Production <see cref="ISecretProvider"/>: reads a secret from the process
/// environment (a plain env var, or a Docker/Kubernetes secret mounted and
/// exported as one at container start — both arrive to .NET the same way).
/// A secret name is upper-cased and hyphens become underscores, then
/// prefixed, so <c>envelope-master-key</c> resolves
/// <c>ALKAROS_SECRET_ENVELOPE_MASTER_KEY</c>. Never reads from settings or
/// the database (<see cref="ISecretProvider"/>'s own contract).
/// </summary>
public sealed class EnvironmentVariableSecretProvider : ISecretProvider
{
    private const string VariablePrefix = "ALKAROS_SECRET_";

    /// <inheritdoc/>
    public string? GetValue(SecretReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        var variableName = VariablePrefix + reference.Name.ToUpperInvariant().Replace('-', '_');
        var value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
