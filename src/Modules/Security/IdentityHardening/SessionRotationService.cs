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

        // A rotation replaces the token, it never extends the session: the replacement ends no later than the session it
        // replaces, so repeated unlocks cannot keep one login alive forever.
        // One second is held back for the time between this read and the clock read inside the creation, so the
        // replacement can only end earlier, never later.
        var remaining = current.ExpiresAt - DateTimeOffset.UtcNow - TimeSpan.FromSeconds(1);
        if (remaining < TimeSpan.FromMilliseconds(1))
            remaining = TimeSpan.FromMilliseconds(1);
        var effectiveLifetime = lifetime is { } requested && requested < remaining ? requested : remaining;
        var (newSession, rawToken) = await _sessions.CreateSessionAsync(userId, deviceId, effectiveLifetime, cancellationToken);
        await _sessions.RevokeAsync(current.SessionId, cancellationToken);

        return (newSession, rawToken);
    }
}
