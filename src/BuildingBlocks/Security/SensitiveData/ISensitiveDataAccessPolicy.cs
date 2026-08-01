namespace ALKAROS.SensitiveData;

/// <summary>
/// The authorized-read boundary: decides whether an accessor may decrypt a
/// <see cref="SensitiveEnvelope"/>. The check runs before decryption, so a
/// denied accessor cannot observe payload existence, key validity or
/// ciphertext integrity.
/// </summary>
public interface ISensitiveDataAccessPolicy
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="accessor"/> is permitted to
    /// read <paramref name="envelope"/>.
    /// </summary>
    bool CanRead(string accessor, SensitiveEnvelope envelope);
}
