namespace ALKAROS.SensitiveData;

/// <summary>
/// Raised when encryption or decryption fails: a malformed envelope key or
/// a failed integrity check. The failure is fail-closed — no plaintext is
/// ever returned or included in the message.
/// </summary>
public sealed class SensitiveDataEncryptionException : SensitiveDataException
{
    public SensitiveDataEncryptionException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
