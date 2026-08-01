using System.Text.Json;
using ALKAROS.Secrets;

namespace ALKAROS.SensitiveData;

/// <summary>
/// The sensitive payload boundary facade: protects classified plaintext
/// payloads into <see cref="SensitiveEnvelope"/> ciphertext and unprotects
/// them only for authorized accessors. Classification failures, key
/// failures and unauthorized reads all fail closed — nothing is written or
/// returned when the boundary cannot complete safely.
/// </summary>
public sealed class SensitivePayloadProtector
{
    private readonly IEnvelopeCipher _cipher;
    private readonly ISensitiveDataAccessPolicy _accessPolicy;

    public SensitivePayloadProtector(
        IEnvelopeCipher cipher,
        ISensitiveDataAccessPolicy accessPolicy)
    {
        _cipher = cipher ?? throw new ArgumentNullException(nameof(cipher));
        _accessPolicy = accessPolicy ?? throw new ArgumentNullException(nameof(accessPolicy));
    }

    /// <summary>
    /// Encrypts a classified payload into a persistable envelope. The
    /// returned envelope contains ciphertext only; on any failure no
    /// envelope is produced.
    /// </summary>
    public SensitiveEnvelope Protect(
        SensitivePayload payload,
        SecretReference key,
        string accessor)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessor);

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload);
        var ciphertext = _cipher.Encrypt(key, accessor, plaintext);
        return new SensitiveEnvelope(payload.Categories, ciphertext, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Decrypts an envelope back into the classified payload, but only when
    /// <paramref name="accessor"/> is authorized. Authorization is checked
    /// before decryption, so a denied accessor never reaches the cipher.
    /// </summary>
    /// <exception cref="UnauthorizedSensitiveReadException">
    /// The accessor is not permitted to read the envelope.
    /// </exception>
    public SensitivePayload Unprotect(
        SensitiveEnvelope envelope,
        SecretReference key,
        string accessor)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessor);

        if (!_accessPolicy.CanRead(accessor, envelope))
            throw new UnauthorizedSensitiveReadException(accessor);

        var plaintext = _cipher.Decrypt(key, accessor, envelope.Ciphertext);
        return JsonSerializer.Deserialize<SensitivePayload>(plaintext)
            ?? throw new SensitiveDataException(
                "Payload failed to deserialize; it was not written by this boundary.");
    }
}
