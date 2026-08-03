using ALKAROS.Secrets;

namespace ALKAROS.SensitiveData;

/// <summary>
/// The envelope encryption contract: encrypts plaintext into an
/// <see cref="EnvelopeCiphertext"/> and decrypts it back. Keys are resolved
/// through the secret resolution boundary, so a missing or unauthorized key
/// fails closed before any plaintext or ciphertext is produced.
/// </summary>
public interface IEnvelopeCipher
{
    /// <summary>
    /// Encrypts <paramref name="plaintext"/> with the key resolved from
    /// <paramref name="key"/>.
    /// </summary>
    EnvelopeCiphertext Encrypt(
        SecretReference key,
        string accessor,
        ReadOnlyMemory<byte> plaintext,
        ReadOnlyMemory<byte> associatedData);

    /// <summary>
    /// Decrypts <paramref name="ciphertext"/> with the key resolved from
    /// <paramref name="key"/>. Integrity failures raise
    /// <see cref="SensitiveDataEncryptionException"/>.
    /// </summary>
    byte[] Decrypt(
        SecretReference key,
        string accessor,
        EnvelopeCiphertext ciphertext,
        ReadOnlyMemory<byte> associatedData);
}
