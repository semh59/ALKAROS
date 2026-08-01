using System.Security.Cryptography;
using ALKAROS.Secrets;

namespace ALKAROS.SensitiveData;

/// <summary>
/// AES-256-GCM envelope cipher backed by the .NET cryptographic primitives.
/// Each encryption uses a fresh random nonce and a 16-byte authentication
/// tag, so tampering or a wrong key fails the integrity check and surfaces
/// as a typed <see cref="SensitiveDataEncryptionException"/>.
/// </summary>
public sealed class AesGcmEnvelopeCipher : IEnvelopeCipher
{
    private const int NonceLengthBytes = 12;
    private const int TagLengthBytes = 16;

    private readonly ISecretResolver _secrets;

    public AesGcmEnvelopeCipher(ISecretResolver secrets)
    {
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
    }

    /// <inheritdoc/>
    public EnvelopeCiphertext Encrypt(
        SecretReference key,
        string accessor,
        ReadOnlyMemory<byte> plaintext)
    {
        ArgumentNullException.ThrowIfNull(key);

        var keyBytes = ResolveKey(key, accessor);
        var nonce = RandomNumberGenerator.GetBytes(NonceLengthBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagLengthBytes];

        try
        {
            using (var aes = new AesGcm(keyBytes, TagLengthBytes))
            {
                aes.Encrypt(nonce, plaintext.Span, ciphertext, tag);
            }
        }
        catch (CryptographicException exception)
        {
            throw new SensitiveDataEncryptionException(
                "Envelope encryption failed; no ciphertext was produced.",
                exception);
        }

        return new EnvelopeCiphertext(key.Name, nonce, ciphertext, tag);
    }

    /// <inheritdoc/>
    public byte[] Decrypt(
        SecretReference key,
        string accessor,
        EnvelopeCiphertext ciphertext)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(ciphertext);

        var keyBytes = ResolveKey(key, accessor);
        var plaintext = new byte[ciphertext.Ciphertext.Length];

        try
        {
            using var aes = new AesGcm(keyBytes, TagLengthBytes);
            aes.Decrypt(ciphertext.Nonce, ciphertext.Ciphertext, ciphertext.Tag, plaintext);
        }
        catch (CryptographicException exception)
        {
            throw new SensitiveDataEncryptionException(
                "Envelope decryption failed; ciphertext integrity check failed.",
                exception);
        }

        return plaintext;
    }

    private byte[] ResolveKey(SecretReference key, string accessor)
    {
        using var secret = _secrets.Resolve(key, accessor);
        return EnvelopeKeyCodec.Decode(secret.Value);
    }
}
