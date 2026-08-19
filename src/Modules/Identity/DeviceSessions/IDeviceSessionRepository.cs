namespace ALKAROS.Identity.DeviceSessions;

public interface IDeviceSessionRepository
{
    Task CreateAsync(DeviceSession session, CancellationToken cancellationToken = default);

    Task<DeviceSession?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task UpdateLastSeenAsync(Guid sessionId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(Guid sessionId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    Task<int> RevokeForDeviceAsync(Guid userId, string deviceId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> AddProcessedOperationsAsync(Guid sessionId, IReadOnlyList<PendingOperation> operations, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetProcessedOperationIdsAsync(CancellationToken cancellationToken = default);
}