namespace ALKAROS.Identity.Authentication;

/// <summary>
/// Outcome of a login attempt. Failed attempts deliberately do not distinguish
/// an unknown username from a wrong password, and an inactive account fails
/// with the same reason, so no credential information leaks to the caller.
/// </summary>
public abstract record LoginResult;

public sealed record LoginSuccess(
    Guid UserId,
    string DisplayName,
    IssuedSessionToken Session) : LoginResult;

public enum LoginFailureReason
{
    InvalidCredentials,
    LockedOut,
}

/// <summary>
/// V1-RMD-151: outcome of unlocking an idle device. Unlike a login this can
/// safely say the PIN was never set — the caller already holds a valid
/// session for that user, so nothing is revealed to a stranger.
/// </summary>
public abstract record UnlockResult;

public sealed record UnlockSuccess(Guid UserId, string DisplayName) : UnlockResult;

public enum UnlockFailureReason
{
    InvalidPin,
    LockedOut,
    PinNotSet,
}

public sealed record UnlockFailure(UnlockFailureReason Reason) : UnlockResult;

public sealed record LoginFailure(LoginFailureReason Reason) : LoginResult;
