namespace ALKAROS.Identity.Authentication;

/// <summary>
/// Immutable view of a user row as seen by the authentication service.
/// </summary>
/// <remarks>
/// V1-RMD-151: the PIN fields are deliberately separate from the password
/// ones. A locked PIN must never lock the account — a waiter who forgot their
/// PIN still signs in with username and password — and PIN guesses must not
/// eat the password lockout budget. <c>PinHash</c> is null for a user who has
/// not set one, and unlocking is then simply unavailable.
/// </remarks>
public sealed record StoredUser(
    Guid UserId,
    string Username,
    string PasswordHash,
    string DisplayName,
    bool Active,
    int FailedLoginAttempts,
    DateTimeOffset? LockedUntil,
    DateTimeOffset? LastLoginAt,
    string? PinHash = null,
    int PinFailedAttempts = 0,
    DateTimeOffset? PinLockedUntil = null);
