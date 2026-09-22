using System.Globalization;
using ALKAROS.Secrets;

namespace ALKAROS.Security.SecretRotation;

/// <summary>
/// Builds the versioned <see cref="SecretReference"/> a rotation version
/// resolves through the existing <see cref="ISecretResolver"/> boundary —
/// e.g. secret name <c>envelope-master-key</c> version 3 resolves reference
/// <c>envelope-master-key-v3</c>, which <see cref="EnvironmentVariableSecretProvider"/>
/// in turn maps to <c>ALKAROS_SECRET_ENVELOPE_MASTER_KEY_V3</c>.
/// </summary>
public static class SecretVersionReferenceNaming
{
    public static SecretReference ReferenceFor(string secretName, int version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);
        return new SecretReference($"{secretName}-v{version.ToString(CultureInfo.InvariantCulture)}");
    }
}
