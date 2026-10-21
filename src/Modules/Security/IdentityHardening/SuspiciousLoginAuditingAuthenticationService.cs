using ALKAROS.Identity.Authentication;

namespace ALKAROS.Security.IdentityHardening;

/// <summary>
/// Wraps <see cref="AuthenticationService"/> to audit two signals without
/// touching lockout mechanics at all: a login that only succeeded after a
/// run of prior failures, and an attempt rejected because the account is
/// already locked. Every outcome (including the lockout/rate-limit decision
/// itself) is delegated verbatim to <see cref="AuthenticationService"/>, so
/// this decorator cannot change which accounts get locked or when — it only
/// observes and reports.
/// </summary>
public sealed class SuspiciousLoginAuditingAuthenticationService
{
    /// <summary>
    /// A success is audited once at least this many failures preceded it.
    /// Deliberately below <see cref="AuthenticationService.DefaultMaxFailedAttempts"/>
    /// so a near-miss brute-force run is visible even when it never trips
    /// the lockout itself.
    /// </summary>
    public const int DefaultSuspiciousFailureThreshold = 3;

    private readonly AuthenticationService _inner;
    private readonly IUserStore _store;
    private readonly ISuspiciousLoginAuditSink _sink;
    private readonly int _suspiciousFailureThreshold;

    public SuspiciousLoginAuditingAuthenticationService(
        AuthenticationService inner,
        IUserStore store,
        ISuspiciousLoginAuditSink sink,
        int suspiciousFailureThreshold = DefaultSuspiciousFailureThreshold)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _suspiciousFailureThreshold = suspiciousFailureThreshold > 0
            ? suspiciousFailureThreshold
            : throw new ArgumentOutOfRangeException(nameof(suspiciousFailureThreshold), "Must be positive.");
    }

    public async Task<LoginResult> LoginAsync(
        string username,
        string password,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(username);

        // Read-only lookup: AuthenticationService.LoginAsync repeats the same
        // GetByUsernameAsync internally, so this never races the real
        // decision and never mutates state on its own.
        var before = await _store.GetByUsernameAsync(username, cancellationToken);
        var priorFailedAttempts = before?.FailedLoginAttempts ?? 0;

        var result = await _inner.LoginAsync(username, password, now, cancellationToken);

        SuspiciousLoginEvent? signal = null;
        switch (result)
        {
            case LoginSuccess success when priorFailedAttempts >= _suspiciousFailureThreshold:
                signal = new SuspiciousLoginEvent(
                    success.UserId, username, SuspiciousLoginReason.SuccessAfterRepeatedFailures, priorFailedAttempts, now);
                break;

            // The attempt that arms the lock is the one worth a record; every later attempt on the locked account is
            // not, or a single guessing run would flood the audit log.
            case LoginFailure { Reason: LoginFailureReason.InvalidCredentials } when before is not null && !IsLocked(before, now):
                var after = await _store.GetByUsernameAsync(username, cancellationToken);
                if (after is not null && IsLocked(after, now))
                {
                    signal = new SuspiciousLoginEvent(
                        before.UserId, username, SuspiciousLoginReason.LockoutTriggered, after.FailedLoginAttempts, now);
                }

                break;
        }

        if (signal is not null)
        {
            try
            {
                await _sink.RecordAsync(signal, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The sign-in has already been decided; a failing audit sink must not turn it into an error.
            }
        }

        return result;
    }

    private static bool IsLocked(StoredUser user, DateTimeOffset now) => user.LockedUntil is { } until && until > now;
}
