namespace ALKAROS.Identity.DeviceSessions;

public sealed class DeviceSessionService : IDeviceSessionService
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(30);

    private readonly IDeviceSessionRepository _repository;

    public DeviceSessionService(IDeviceSessionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<(DeviceSession Session, string RawToken)> CreateSessionAsync(
        Guid userId,
        string deviceId,
        TimeSpan? lifetime = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId);

        var (raw, hash) = DeviceSessionToken.Create();
        var now = DateTimeOffset.UtcNow;
        var session = new DeviceSession(
            SessionId: Guid.NewGuid(),
            UserId: userId,
            DeviceId: deviceId,
            TokenHash: hash,
            CreatedAt: now,
            ExpiresAt: now + (lifetime ?? DefaultLifetime),
            RevokedAt: null,
            LastSeenAt: null);

        await _repository.CreateAsync(session, cancellationToken);
        return (session, raw);
    }

    public Task<DeviceSession> AuthenticateAsync(
        Guid userId,
        string deviceId,
        string rawToken,
        CancellationToken cancellationToken = default)
        => ValidateAsync(userId, deviceId, rawToken, touchLastSeen: true, cancellationToken);

    public Task<bool> RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default)
        => _repository.RevokeAsync(sessionId, DateTimeOffset.UtcNow, cancellationToken);

    public Task<int> RevokeDeviceAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId);

        return _repository.RevokeForDeviceAsync(userId, deviceId, DateTimeOffset.UtcNow, cancellationToken);
    }

    public async Task<ReconnectResult> ReconnectAsync(
        Guid userId,
        string deviceId,
        string rawToken,
        IReadOnlyList<PendingOperation> pendingOperations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pendingOperations);

        var session = await ValidateAsync(userId, deviceId, rawToken, touchLastSeen: false, cancellationToken);
        if (pendingOperations.Count == 0)
        {
            return new ReconnectResult(session, Array.Empty<PendingOperation>());
        }

        var candidateOps = pendingOperations
            .OrderBy(op => op.QueuedAt)
            .ThenBy(op => op.OperationId)
            .ToList();

        var insertedIds = (await _repository.AddProcessedOperationsAsync(session.SessionId, candidateOps, cancellationToken))
            .ToHashSet();

        var applied = candidateOps
            .Where(op => insertedIds.Contains(op.OperationId))
            .ToList();

        return new ReconnectResult(session, applied);
    }

    private async Task<DeviceSession> ValidateAsync(
        Guid userId,
        string deviceId,
        string rawToken,
        bool touchLastSeen,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId);
        ArgumentException.ThrowIfNullOrEmpty(rawToken);

        var tokenHash = DeviceSessionToken.Hash(rawToken);
        var session = await _repository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (session is null || session.UserId != userId || session.DeviceId != deviceId)
        {
            throw new InvalidSessionTokenException(
                "Session does not exist or the token does not belong to this user/device.");
        }

        if (session.RevokedAt is not null)
        {
            throw new DeviceSessionRevokedException($"Session {session.SessionId} was revoked.");
        }

        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new DeviceSessionExpiredException($"Session {session.SessionId} has expired.");
        }

        if (touchLastSeen)
        {
            await _repository.UpdateLastSeenAsync(session.SessionId, DateTimeOffset.UtcNow, cancellationToken);
        }

        return session;
    }
}