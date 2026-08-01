namespace ALKAROS.SensitiveData;

/// <summary>
/// Base class for every typed failure raised by the sensitive payload
/// boundary. Messages carry identifiers and never plaintext payload values.
/// </summary>
public class SensitiveDataException : Exception
{
    public SensitiveDataException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
