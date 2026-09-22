using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Security.IdentityHardening.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Security.IdentityHardening.Tests;

public sealed class SessionRotationServiceTests : IClassFixture<IdentityHardeningTestDatabase>
{
    private const string Device = "pos-terminal-001";

    private readonly IdentityHardeningTestDatabase _database;
    private readonly DeviceSessionService _sessions;
    private readonly SessionRotationService _rotation;

    public SessionRotationServiceTests(IdentityHardeningTestDatabase database)
    {
        _database = database;
        _sessions = new DeviceSessionService(new PostgresDeviceSessionRepository(database.DataSource));
        _rotation = new SessionRotationService(_sessions);
    }

    [Fact]
    public async Task RotateIssuesANewTokenAndImmediatelyFailsTheOldOne()
    {
        var userId = await _database.InsertUserAsync();
        var (original, originalToken) = await _sessions.CreateSessionAsync(userId, Device);

        var (rotated, rotatedToken) = await _rotation.RotateAsync(userId, Device, originalToken);

        rotated.SessionId.Should().NotBe(original.SessionId);
        rotatedToken.Should().NotBe(originalToken);

        // The old token fails immediately — no overlap window.
        await Assert.ThrowsAsync<DeviceSessionRevokedException>(
            () => _sessions.AuthenticateAsync(userId, Device, originalToken));

        // The new token works.
        var authenticated = await _sessions.AuthenticateAsync(userId, Device, rotatedToken);
        authenticated.SessionId.Should().Be(rotated.SessionId);
    }

    [Fact]
    public async Task RotateRejectsAnAlreadyRevokedToken()
    {
        var userId = await _database.InsertUserAsync();
        var (session, token) = await _sessions.CreateSessionAsync(userId, Device);
        await _sessions.RevokeAsync(session.SessionId);

        await Assert.ThrowsAsync<DeviceSessionRevokedException>(
            () => _rotation.RotateAsync(userId, Device, token));
    }

    [Fact]
    public async Task RotateRejectsAnUnknownToken()
    {
        var userId = await _database.InsertUserAsync();

        await Assert.ThrowsAsync<InvalidSessionTokenException>(
            () => _rotation.RotateAsync(userId, Device, "not-a-real-token"));
    }
}
