namespace ALKAROS.Security.IdentityHardening;

/// <summary>
/// A login-time signal worth an administrator's attention. Never carries a
/// password, token or hash — only identifiers and counters.
/// </summary>
public enum SuspiciousLoginReason
{
    /// <summary>A login succeeded only after a run of prior failures on the same account.</summary>
    SuccessAfterRepeatedFailures,

    /// <summary>A login attempt was rejected because the account is currently locked out.</summary>
    LockoutTriggered,

    /// <summary>An administrator revoked every active session for a user.</summary>
    AllSessionsRevoked,

    /// <summary>An administrator force-unlocked an account before its lockout window expired.</summary>
    AccountForceUnlocked,

    /// <summary>V1-RMD-405: an administrator deactivated an account (a leaver); its sessions were revoked.</summary>
    AccountDeactivated,

    /// <summary>V1-RMD-405: an administrator reactivated a previously deactivated account.</summary>
    AccountReactivated,
}

public sealed record SuspiciousLoginEvent(
    Guid? UserId,
    string Username,
    SuspiciousLoginReason Reason,
    int PriorFailedAttempts,
    DateTimeOffset OccurredAt,
    string? Actor = null);
