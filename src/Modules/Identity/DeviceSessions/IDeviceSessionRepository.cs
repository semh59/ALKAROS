namespace ALKAROS.Identity.DeviceSessions;

public interface IDeviceSessionRepository
{
    Task CreateAsync(DeviceSession session, CancellationToken cancellationToken = default);

    Task<DeviceSession?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task UpdateLastSeenAsync(Guid sessionId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(Guid sessionId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    Task<int> RevokeForDeviceAsync(Guid userId, string deviceId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    Task<ReconnectClaimResult> ClaimReconnectOperationsAsync(
        string tokenHash,
        Guid userId,
        string deviceId,
        IReadOnlyList<PendingOperation> operations,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetProcessedOperationIdsAsync(CancellationToken cancellationToken = default);
}

public enum ReconnectClaimStatus
{
    Success,
    InvalidSession,
    Revoked,
    Expired,
}

public sealed record ReconnectClaimResult(
    ReconnectClaimStatus Status,
    DeviceSession? Session,
    IReadOnlyList<Guid> InsertedOperationIds);
