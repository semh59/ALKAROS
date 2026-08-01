namespace ALKAROS.SensitiveData;

/// <summary>
/// Converts a secret value into the raw symmetric key bytes used by the
/// envelope cipher. Keys are 256-bit (32 bytes) values transported in
/// base64 form through the secret resolution boundary.
/// </summary>
public static class EnvelopeKeyCodec
{
    private const int KeyLengthBytes = 32;

    /// <summary>
    /// Decodes a base64 secret value into the 32-byte AES-256 key.
    /// </summary>
    /// <exception cref="SensitiveDataEncryptionException">
    /// The value is not valid base64 or does not decode to exactly 32 bytes.
    /// </exception>
    public static byte[] Decode(string base64Value)
    {
        ArgumentNullException.ThrowIfNull(base64Value);

        byte[] key;
        try
        {
            key = Convert.FromBase64String(base64Value);
        }
        catch (FormatException exception)
        {
            throw new SensitiveDataEncryptionException(
                "Envelope key is not valid base64.", exception);
        }

        if (key.Length != KeyLengthBytes)
            throw new SensitiveDataEncryptionException(
                $"Envelope key must decode to {KeyLengthBytes} bytes.");

        return key;
    }
}
