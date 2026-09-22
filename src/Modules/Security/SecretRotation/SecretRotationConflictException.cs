namespace ALKAROS.Security.SecretRotation;

/// <summary>
/// Thrown when a requested rotation state transition would violate an
/// invariant (unknown version, wrong status for the requested transition,
/// expired overlap window).
/// </summary>
public sealed class SecretRotationConflictException : Exception
{
    public SecretRotationConflictException(string message) : base(message)
    {
    }
}
