namespace ALKAROS.SensitiveData;

/// <summary>
/// The output of envelope encryption: the encrypted payload bytes together
/// with the encryption parameters needed to decrypt them. The envelope
/// carries the ciphertext only — no plaintext — so it can be persisted or
/// transported without exposing the payload.
/// </summary>
public sealed record EnvelopeCiphertext
{
    /// <summary>
    /// Identifier of the key that produced this ciphertext.
    /// </summary>
    public string KeyId { get; init; }

    /// <summary>
    /// Random per-encryption nonce.
    /// </summary>
    public byte[] Nonce { get; init; }

    /// <summary>
    /// The encrypted payload bytes.
    /// </summary>
    public byte[] Ciphertext { get; init; }

    /// <summary>
    /// Authentication tag that proves integrity and authenticates the
    /// ciphertext against the key.
    /// </summary>
    public byte[] Tag { get; init; }

    public EnvelopeCiphertext(
        string keyId,
        byte[] nonce,
        byte[] ciphertext,
        byte[] tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentNullException.ThrowIfNull(ciphertext);
        ArgumentNullException.ThrowIfNull(tag);

        KeyId = keyId;
        Nonce = nonce;
        Ciphertext = ciphertext;
        Tag = tag;
    }
}
