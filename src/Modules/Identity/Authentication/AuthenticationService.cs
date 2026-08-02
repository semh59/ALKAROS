namespace ALKAROS.Identity.Authentication;

/// <summary>
/// Application service for username/password login. Enforces the active-user
/// rule, records failed attempts, applies a temporary lockout after
/// <see cref="MaxFailedAttempts"/> consecutive failures, and issues a
/// stateless secure session token on success. Logout is client-side token
/// disposal; server-side revocation lives in V1-IAM-003 (device_sessions).
/// </summary>
public sealed class AuthenticationService
{
    public const int DefaultMaxFailedAttempts = 5;
    public static readonly TimeSpan DefaultLockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IUserStore _store;
    private readonly int _maxFailedAttempts;
    private readonly TimeSpan _lockoutDuration;

    public AuthenticationService(
        IUserStore store,
        int maxFailedAttempts = DefaultMaxFailedAttempts,
        TimeSpan? lockoutDuration = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _maxFailedAttempts = maxFailedAttempts > 0
            ? maxFailedAttempts
            : throw new ArgumentOutOfRangeException(nameof(maxFailedAttempts), "Must be positive.");
        _lockoutDuration = lockoutDuration ?? DefaultLockoutDuration;
        if (_lockoutDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lockoutDuration), "Must be positive.");
    }

    /// <summary>
    /// Attempts to log <paramref name="username"/> in with
    /// <paramref name="password"/> at <paramref name="now"/>. A locked account
    /// is rejected without touching the failure counter; every other failure
    /// increments it and may arm the lock.
    /// </summary>
    public async Task<LoginResult> LoginAsync(
        string username,
        string password,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        var user = await _store.GetByUsernameAsync(username, cancellationToken);
        if (user is null || !user.Active)
            return new LoginFailure(LoginFailureReason.InvalidCredentials);

        if (user.LockedUntil is { } effectiveLock && effectiveLock > now)
            return new LoginFailure(LoginFailureReason.LockedOut);

        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            var update = await _store.RecordLoginFailureAsync(
                user.UserId, now, _maxFailedAttempts, _lockoutDuration, cancellationToken);
            if (update is null)
                return new LoginFailure(LoginFailureReason.LockedOut);

            return new LoginFailure(LoginFailureReason.InvalidCredentials);
        }

        if (!await _store.RecordLoginSuccessAsync(user.UserId, now, cancellationToken))
            return new LoginFailure(LoginFailureReason.LockedOut);
        var session = SessionTokenIssuer.Issue(now);
        return new LoginSuccess(user.UserId, user.DisplayName, session);
    }
}
