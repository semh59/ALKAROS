namespace ALKAROS.Identity.DeviceSessions;

/// <summary>
/// Device-bound session lifecycle (V1-IAM-003): creation returns a raw token
/// exactly once, every persisted token is a SHA-256 hash, sessions are bound
/// to a single device, and reconnect preserves only the permitted queued
/// operations (V0-ARC-002 rules).
/// </summary>
public interface IDeviceSessionService
{
    Task<(DeviceSession Session, string RawToken)> CreateSessionAsync(
        Guid userId,
        string deviceId,
        TimeSpan? lifetime = null,
        CancellationToken cancellationToken = default);

    Task<DeviceSession> AuthenticateAsync(
        Guid userId,
        string deviceId,
        string rawToken,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<int> RevokeDeviceAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default);

    Task<ReconnectResult> ReconnectAsync(
        Guid userId,
        string deviceId,
        string rawToken,
        IReadOnlyList<PendingOperation> pendingOperations,
        CancellationToken cancellationToken = default);
}