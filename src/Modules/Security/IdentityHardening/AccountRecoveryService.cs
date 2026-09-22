using ALKAROS.Identity.Authentication;
using ALKAROS.Identity.DeviceSessions;

namespace ALKAROS.Security.IdentityHardening;

/// <summary>
/// Administrative recovery actions — deliberately separate from a user's own
/// self-service unlock (<see cref="AuthenticationService.UnlockAsync"/>,
/// PIN-only). Every action here requires a specific user id already known to
/// the caller (an authorized operator), never a username lookup by a
/// stranger, and every action is audited.
/// </summary>
public sealed class AccountRecoveryService
{
    private readonly IUserStore _store;
    private readonly IDeviceSessionService _sessions;
    private readonly ISuspiciousLoginAuditSink _sink;

    public AccountRecoveryService(IUserStore store, IDeviceSessionService sessions, ISuspiciousLoginAuditSink sink)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    /// <summary>
    /// "Sign out everywhere": revokes every active device session for
    /// <paramref name="userId"/>. Each revoked session fails its next
    /// <see cref="IDeviceSessionService.AuthenticateAsync"/> call immediately
    /// (<see cref="DeviceSessionRevokedException"/>) — there is no grace
    /// window.
    /// </summary>
    public async Task<int> RevokeAllSessionsAsync(Guid userId, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        var user = await _store.GetByIdAsync(userId, cancellationToken);
        var revokedCount = await _sessions.RevokeAllAsync(userId, cancellationToken);

        await _sink.RecordAsync(
            new SuspiciousLoginEvent(
                userId,
                user?.Username ?? userId.ToString(),
                SuspiciousLoginReason.AllSessionsRevoked,
                PriorFailedAttempts: 0,
                DateTimeOffset.UtcNow,
                Actor: actor),
            cancellationToken);

        return revokedCount;
    }

    /// <summary>
    /// Clears a lockout before its window would naturally expire. Returns
    /// <c>false</c> when no such user exists; a user who was not locked is
    /// unlocked idempotently (the counters are reset regardless).
    /// </summary>
    public async Task<bool> ForceUnlockAsync(Guid userId, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        var user = await _store.GetByIdAsync(userId, cancellationToken);
        var unlocked = await _store.ForceUnlockAsync(userId, cancellationToken);
        if (!unlocked)
            return false;

        await _sink.RecordAsync(
            new SuspiciousLoginEvent(
                userId,
                user?.Username ?? userId.ToString(),
                SuspiciousLoginReason.AccountForceUnlocked,
                PriorFailedAttempts: user?.FailedLoginAttempts ?? 0,
                DateTimeOffset.UtcNow,
                Actor: actor),
            cancellationToken);

        return true;
    }
}
