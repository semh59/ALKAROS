namespace ALKAROS.Identity.Authentication;

/// <summary>
/// Verifies a raw password against an encoded PBKDF2 hash. The production
/// implementation is <see cref="PasswordHasher.Verify"/>; the seam exists so
/// the login work contract (see docs/engineering/login-timing-contract.md)
/// can be proven deterministically in tests without real PBKDF2 computation.
/// </summary>
public delegate bool PasswordVerifier(string password, string encodedHash);

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
    private readonly PasswordVerifier _verify;

    public AuthenticationService(
        IUserStore store,
        int maxFailedAttempts = DefaultMaxFailedAttempts,
        TimeSpan? lockoutDuration = null,
        PasswordVerifier? verifier = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _maxFailedAttempts = maxFailedAttempts > 0
            ? maxFailedAttempts
            : throw new ArgumentOutOfRangeException(nameof(maxFailedAttempts), "Must be positive.");
        _lockoutDuration = lockoutDuration ?? DefaultLockoutDuration;
        if (_lockoutDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lockoutDuration), "Must be positive.");
        _verify = verifier ?? PasswordHasher.Verify;
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
        {
            // Burn the same PBKDF2 work as a real credential check so
            // unknown and inactive usernames cannot be distinguished from
            // active accounts by response time.
            _verify(password, PasswordHasher.DummyHash);
            return new LoginFailure(LoginFailureReason.InvalidCredentials);
        }

        if (user.LockedUntil is { } effectiveLock && effectiveLock > now)
            return new LoginFailure(LoginFailureReason.LockedOut);

        if (!_verify(password, user.PasswordHash))
        {
            var update = await _store.RecordLoginFailureAsync(
                user.UserId, now, _maxFailedAttempts, _lockoutDuration, cancellationToken);
            if (update is null)
                return new LoginFailure(LoginFailureReason.LockedOut);

            return new LoginFailure(LoginFailureReason.InvalidCredentials);
        }

        if (!await _store.RecordLoginSuccessAsync(user.UserId, now, cancellationToken))
            return new LoginFailure(LoginFailureReason.LockedOut);

        if (PasswordHasher.NeedsRehash(user.PasswordHash))
        {
            var upgradedHash = new PasswordHasher().Hash(password);
            await _store.TryUpgradePasswordHashAsync(
                user.UserId, user.PasswordHash, upgradedHash, cancellationToken);
        }

        var session = SessionTokenIssuer.Issue(now);
        return new LoginSuccess(user.UserId, user.DisplayName, session);
    }

    /// <summary>
    /// V1-RMD-151: verifies the unlock PIN of a user who already holds a valid
    /// device session. This is not authentication — the caller has already
    /// proved which session it is; the PIN only proves the same person is
    /// still holding the device after it sat idle.
    ///
    /// The PIN's own counters are used, so five wrong PINs lock the PIN for
    /// the lockout window without touching the password lockout: someone who
    /// forgot their PIN signs in with username and password as usual, and PIN
    /// guessing cannot lock a colleague out of the account.
    /// </summary>
    public async Task<UnlockResult> UnlockAsync(
        Guid userId,
        string pin,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pin);

        var user = await _store.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.Active)
            return new UnlockFailure(UnlockFailureReason.InvalidPin);

        if (user.PinHash is null)
            return new UnlockFailure(UnlockFailureReason.PinNotSet);

        if (user.PinLockedUntil is { } lockedUntil && lockedUntil > now)
            return new UnlockFailure(UnlockFailureReason.LockedOut);

        if (!_verify(pin, user.PinHash))
        {
            var update = await _store.RecordPinFailureAsync(
                user.UserId, now, _maxFailedAttempts, _lockoutDuration, cancellationToken);
            return update is null
                ? new UnlockFailure(UnlockFailureReason.LockedOut)
                : new UnlockFailure(UnlockFailureReason.InvalidPin);
        }

        if (!await _store.RecordPinSuccessAsync(user.UserId, cancellationToken))
            return new UnlockFailure(UnlockFailureReason.LockedOut);

        return new UnlockSuccess(user.UserId, user.DisplayName);
    }

    /// <summary>
    /// V1-RMD-151: sets or clears a user's own unlock PIN. The current
    /// password is required even though a session already exists — a device
    /// left unlocked must not be enough to plant a PIN on someone's account.
    /// Passing null for <paramref name="pin"/> removes it, which is how a user
    /// turns the PIN prompt back off.
    /// </summary>
    public async Task<bool> SetPinAsync(
        Guid userId,
        string currentPassword,
        string? pin,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPassword);

        var user = await _store.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.Active || !_verify(currentPassword, user.PasswordHash))
            return false;

        var encoded = pin is null ? null : new PasswordHasher().Hash(pin);
        return await _store.SetPinAsync(userId, encoded, cancellationToken);
    }
}
