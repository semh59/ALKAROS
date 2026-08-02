namespace ALKAROS.Identity.Authentication;

/// <summary>
/// Immutable view of a user row as seen by the authentication service.
/// </summary>
public sealed record StoredUser(
    Guid UserId,
    string Username,
    string PasswordHash,
    string DisplayName,
    bool Active,
    int FailedLoginAttempts,
    DateTimeOffset? LockedUntil,
    DateTimeOffset? LastLoginAt);
