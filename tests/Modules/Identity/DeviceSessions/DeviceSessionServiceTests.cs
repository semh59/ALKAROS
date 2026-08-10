using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Identity.DeviceSessions.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Identity.DeviceSessions.Tests;

public sealed class DeviceSessionServiceTests : IClassFixture<DeviceSessionsTestDatabase>
{
    private readonly DeviceSessionsTestDatabase _database;
    private readonly DeviceSessionService _service;

    public DeviceSessionServiceTests(DeviceSessionsTestDatabase database)
    {
        _database = database;
        _service = new DeviceSessionService(new PostgresDeviceSessionRepository(database.DataSource));
    }

    private const string DeviceA = "pos-terminal-001";
    private const string DeviceB = "pos-terminal-002";

    private Task<Guid> InsertUserAsync() => _database.InsertUserAsync();

    private async Task<string> TokenHashStoredAsync(Guid sessionId)
        => await _database.ScalarAsync<string>(
            $"SELECT token_hash FROM identity.device_sessions WHERE session_id = '{sessionId}';");

    [Fact]
    public async Task CreateSessionPersistsOnlyTheTokenHash()
    {
        var userId = await InsertUserAsync();

        var (session, rawToken) = await _service.CreateSessionAsync(userId, DeviceA);

        session.TokenHash.Should().Be(DeviceSessionToken.Hash(rawToken));
        session.DeviceId.Should().Be(DeviceA);
        session.RevokedAt.Should().BeNull();
        session.ExpiresAt.Should().BeCloseTo(
            DateTimeOffset.UtcNow + DeviceSessionService.DefaultLifetime,
            TimeSpan.FromSeconds(5));

        var stored = await _database.ScalarAsync<long>(
            "SELECT count(*) FROM identity.device_sessions "
            + $"WHERE token_hash = '{rawToken}';");
        stored.Should().Be(0, "raw tokens must never be persisted");

        var storedHash = await TokenHashStoredAsync(session.SessionId);
        storedHash.Should().Be(session.TokenHash);
    }

    [Fact]
    public async Task CustomLifetimeIsApplied()
    {
        var userId = await InsertUserAsync();

        var (session, _) = await _service.CreateSessionAsync(userId, DeviceA, TimeSpan.FromDays(1));

        session.ExpiresAt.Should().BeCloseTo(
            DateTimeOffset.UtcNow + TimeSpan.FromDays(1),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AuthenticateWithMatchingTokenTouchesLastSeen()
    {
        var userId = await InsertUserAsync();
        var (session, rawToken) = await _service.CreateSessionAsync(userId, DeviceA);

        var authenticated = await _service.AuthenticateAsync(userId, DeviceA, rawToken);

        authenticated.SessionId.Should().Be(session.SessionId);
        var lastSeen = await _database.ScalarAsync<DateTime>(
            $"SELECT last_seen_at FROM identity.device_sessions WHERE session_id = '{session.SessionId}';");
        lastSeen.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AuthenticateWithWrongTokenThrows()
    {
        var userId = await InsertUserAsync();
        await _service.CreateSessionAsync(userId, DeviceA);

        var act = () => _service.AuthenticateAsync(userId, DeviceA, "alkaros-device-session:not-the-token");

        await act.Should().ThrowAsync<InvalidSessionTokenException>();
    }

    [Fact]
    public async Task AuthenticateWithAnotherUsersTokenThrows()
    {
        var userA = await InsertUserAsync();
        var userB = await InsertUserAsync();
        var (_, rawToken) = await _service.CreateSessionAsync(userA, DeviceA);

        var act = () => _service.AuthenticateAsync(userB, DeviceA, rawToken);

        await act.Should().ThrowAsync<InvalidSessionTokenException>();
    }

    [Fact]
    public async Task AuthenticateWithAnotherDeviceTokenThrows()
    {
        var userId = await InsertUserAsync();
        var (_, rawToken) = await _service.CreateSessionAsync(userId, DeviceA);

        var act = () => _service.AuthenticateAsync(userId, DeviceB, rawToken);

        await act.Should().ThrowAsync<InvalidSessionTokenException>();
    }

    [Fact]
    public async Task RevokedSessionCannotAuthenticate()
    {
        var userId = await InsertUserAsync();
        var (session, rawToken) = await _service.CreateSessionAsync(userId, DeviceA);

        (await _service.RevokeAsync(session.SessionId)).Should().BeTrue();
        (await _service.RevokeAsync(session.SessionId)).Should().BeFalse("second revoke affects no row");

        var act = () => _service.AuthenticateAsync(userId, DeviceA, rawToken);

        await act.Should().ThrowAsync<DeviceSessionRevokedException>();
    }

    [Fact]
    public async Task ExpiredSessionCannotAuthenticate()
    {
        var userId = await InsertUserAsync();
        var (raw, hash) = DeviceSessionToken.Create();
        var sessionId = Guid.NewGuid();
        await _database.ExecuteAsync(
            """
            INSERT INTO identity.device_sessions
                (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES
                (@session_id, @user_id, @device_id, @token_hash, @created_at, @expires_at);
            """,
            ("session_id", sessionId),
            ("user_id", userId),
            ("device_id", DeviceA),
            ("token_hash", hash),
            ("created_at", DateTimeOffset.UtcNow.AddDays(-2)),
            ("expires_at", DateTimeOffset.UtcNow.AddMinutes(-1)));

        var act = () => _service.AuthenticateAsync(userId, DeviceA, raw);

        await act.Should().ThrowAsync<DeviceSessionExpiredException>();
    }

    [Fact]
    public async Task RevokeDeviceRevokesOnlyThatDevicesSessions()
    {
        var userId = await InsertUserAsync();
        await _service.CreateSessionAsync(userId, DeviceA);
        await _service.CreateSessionAsync(userId, DeviceA);
        var (_, otherDeviceToken) = await _service.CreateSessionAsync(userId, DeviceB);

        var revoked = await _service.RevokeDeviceAsync(userId, DeviceA);
        revoked.Should().Be(2);

        var remaining = await _service.AuthenticateAsync(userId, DeviceB, otherDeviceToken);
        remaining.DeviceId.Should().Be(DeviceB);
    }

    [Fact]
    public async Task ReconnectReturnsOnlyUnprocessedOperations()
    {
        var userId = await InsertUserAsync();
        var (session, rawToken) = await _service.CreateSessionAsync(userId, DeviceA);
        var op1 = new PendingOperation(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-3));
        var op2 = new PendingOperation(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-2));
        var op3 = new PendingOperation(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1));

        var first = await _service.ReconnectAsync(userId, DeviceA, rawToken, [op1, op2]);

        first.Session.SessionId.Should().Be(session.SessionId);
        first.AppliedOperations.Should().Equal(op1, op2);

        var second = await _service.ReconnectAsync(userId, DeviceA, rawToken, [op1, op2, op3]);

        second.AppliedOperations.Should().Equal(op3);
    }

    [Fact]
    public async Task ReconnectOrdersUnprocessedOperationsByQueuedAt()
    {
        var userId = await InsertUserAsync();
        var (_, rawToken) = await _service.CreateSessionAsync(userId, DeviceA);
        var late = new PendingOperation(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1));
        var early = new PendingOperation(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-10));

        var result = await _service.ReconnectAsync(userId, DeviceA, rawToken, [late, early]);

        result.AppliedOperations.Should().Equal(early, late);
    }

    [Fact]
    public async Task ReconnectWithInvalidTokenThrows()
    {
        var userId = await InsertUserAsync();
        await _service.CreateSessionAsync(userId, DeviceA);

        var act = () => _service.ReconnectAsync(
            userId,
            DeviceA,
            "alkaros-device-session:wrong-token",
            [new PendingOperation(Guid.NewGuid(), DateTimeOffset.UtcNow)]);

        await act.Should().ThrowAsync<InvalidSessionTokenException>();
    }
}