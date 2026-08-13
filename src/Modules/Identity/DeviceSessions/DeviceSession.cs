namespace ALKAROS.Identity.DeviceSessions;

public sealed record DeviceSession(
    Guid SessionId,
    Guid UserId,
    string DeviceId,
    string TokenHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt,
    DateTimeOffset? LastSeenAt);

public sealed record PendingOperation(Guid OperationId, DateTimeOffset QueuedAt);

public sealed record ReconnectResult(DeviceSession Session, IReadOnlyList<PendingOperation> AppliedOperations);