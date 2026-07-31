namespace ALKAROS.Secrets;

/// <summary>
/// Base class for every typed failure raised by the secret resolution
/// boundary. Messages carry the secret reference name and accessor only —
/// secret values are never part of a message, so failures cannot leak
/// credentials into logs or exception surfaces.
/// </summary>
public class SecretResolutionException : Exception
{
    public SecretResolutionException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
