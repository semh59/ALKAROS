namespace ALKAROS.Identity.Authentication;

/// <summary>
/// Persistence contract for user credentials used by authentication.
/// </summary>
public interface IUserStore
{
    /// <summary>
    /// Returns the user matching <paramref name="username"/> or null when no
    /// such user exists. Username lookup is case-sensitive.
    /// </summary>
    Task<StoredUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically records a failed login attempt unless another concurrent
    /// attempt has already armed a lock. A null result means the account is
    /// currently locked.
    /// </summary>
    Task<LoginFailureUpdate?> RecordLoginFailureAsync(
        Guid userId,
        DateTimeOffset now,
        int maxFailedAttempts,
        TimeSpan lockoutDuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a successful login only when no active lock exists.
    /// </summary>
    Task<bool> RecordLoginSuccessAsync(Guid userId, DateTimeOffset lastLoginAt, CancellationToken cancellationToken = default);

    Task<bool> TryUpgradePasswordHashAsync(
        Guid userId,
        string expectedCurrentHash,
        string upgradedHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// V1-RMD-151: reads a user by id — the unlock path already knows who is
    /// asking (it requires a valid device session), so it never looks anyone
    /// up by name.
    /// </summary>
    Task<StoredUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// V1-RMD-151: stores the user's own unlock PIN, or clears it when
    /// <paramref name="encodedPinHash"/> is null. Clearing also resets the
    /// PIN attempt counters, so removing and re-adding a PIN never starts
    /// from a locked state.
    /// </summary>
    Task<bool> SetPinAsync(Guid userId, string? encodedPinHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// V1-RMD-151: mirrors <see cref="RecordLoginFailureAsync"/> for the PIN's
    /// own counters. A null result means the PIN is currently locked.
    /// </summary>
    Task<LoginFailureUpdate?> RecordPinFailureAsync(
        Guid userId,
        DateTimeOffset now,
        int maxFailedAttempts,
        TimeSpan lockoutDuration,
        CancellationToken cancellationToken = default);

    /// <summary>V1-RMD-151: clears the PIN attempt counters after a correct PIN.</summary>
    Task<bool> RecordPinSuccessAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// V15-SEC-002: an authorized administrative action that clears a locked
    /// account immediately instead of waiting out the lockout window — the
    /// account's own password lockout counters only, never the PIN's (same
    /// separation <see cref="SetPinAsync"/> keeps). Returns <c>false</c> when
    /// no such user exists.
    /// </summary>
    Task<bool> ForceUnlockAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record LoginFailureUpdate(int FailedLoginAttempts, DateTimeOffset? LockedUntil);
