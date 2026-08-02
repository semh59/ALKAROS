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

public sealed record LoginFailure(LoginFailureReason Reason) : LoginResult;
