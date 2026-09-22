namespace ALKAROS.Security.SecretRotation;

/// <summary>
/// Persists rotation state (version metadata only — never a secret value)
/// keyed by secret name.
/// </summary>
public interface ISecretRotationStore
{
    /// <summary>Returns the rotation record for <paramref name="secretName"/>, or <c>null</c> if it has never been rotated.</summary>
    SecretRotationRecord? Find(string secretName);

    /// <summary>Persists <paramref name="record"/>, replacing any prior state for the same secret name.</summary>
    void Save(SecretRotationRecord record);
}
