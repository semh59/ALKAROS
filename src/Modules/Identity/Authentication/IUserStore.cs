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
    /// Records a failed login attempt: increments the failure counter and
    /// applies the lock window when <paramref name="lockedUntil"/> is set.
    /// </summary>
    Task RecordLoginFailureAsync(
        Guid userId,
        int failedLoginAttempts,
        DateTimeOffset? lockedUntil,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a successful login: clears the failure counter and the lock,
    /// sets <paramref name="lastLoginAt"/> and bumps the row version.
    /// </summary>
    Task RecordLoginSuccessAsync(Guid userId, DateTimeOffset lastLoginAt, CancellationToken cancellationToken = default);
}
