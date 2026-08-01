namespace ALKAROS.SensitiveData;

/// <summary>
/// Raised when an accessor tries to read a sensitive envelope without
/// permission. This failure is raised before decryption is attempted so
/// that an unauthorized accessor learns nothing about the payload.
/// </summary>
public sealed class UnauthorizedSensitiveReadException : SensitiveDataException
{
    public UnauthorizedSensitiveReadException(string accessor)
        : base($"Accessor '{accessor}' is not allowed to read sensitive payloads.")
    {
    }
}
