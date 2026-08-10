namespace ALKAROS.Identity.Authorization;

/// <summary>
/// Raised when an actor is denied a named permission. The denied operation
/// must not perform any durable change (V1-IAM-002 acceptance).
/// </summary>
public sealed class AuthorizationDeniedException : Exception
{
    public AuthorizationDeniedException(Guid? userId, string permissionCode, string reason)
        : base($"User '{userId}' denied permission '{permissionCode}': {reason}")
    {
        UserId = userId;
        PermissionCode = permissionCode;
        Reason = reason;
    }

    public Guid? UserId { get; }

    public string PermissionCode { get; }

    public string Reason { get; }
}