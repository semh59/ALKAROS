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
}

public sealed record LoginFailureUpdate(int FailedLoginAttempts, DateTimeOffset? LockedUntil);
