using ALKAROS.Identity.DeviceSessions;

namespace ALKAROS.Security.IdentityHardening;

/// <summary>
/// Reissues a device session's token without a full re-login: validates the
/// caller's current token first (fail-closed — an invalid/expired/revoked
/// token never reaches the create step), then creates the replacement and
/// revokes the original. The two writes are not wrapped in a single
/// database transaction — <see cref="DeviceSessionService"/> exposes no such
/// seam — but the ordering (create new, then revoke old) is deliberately
/// safe either way: if the revoke step fails or is never reached, the caller
/// is left with two valid sessions rather than zero, never locked out by a
/// rotation that only half completed.
/// </summary>
public sealed class SessionRotationService
{
    private readonly IDeviceSessionService _sessions;

    public SessionRotationService(IDeviceSessionService sessions)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
    }

    public async Task<(DeviceSession Session, string RawToken)> RotateAsync(
        Guid userId,
        string deviceId,
        string currentRawToken,
        TimeSpan? lifetime = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId);
        ArgumentException.ThrowIfNullOrEmpty(currentRawToken);

        var current = await _sessions.AuthenticateAsync(userId, deviceId, currentRawToken, cancellationToken);

        var (newSession, rawToken) = await _sessions.CreateSessionAsync(userId, deviceId, lifetime, cancellationToken);
        await _sessions.RevokeAsync(current.SessionId, cancellationToken);

        return (newSession, rawToken);
    }
}
