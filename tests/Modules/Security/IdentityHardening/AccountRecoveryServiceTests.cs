using ALKAROS.Identity.Authentication;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Security.IdentityHardening.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Security.IdentityHardening.Tests;

public sealed class AccountRecoveryServiceTests : IClassFixture<IdentityHardeningTestDatabase>
{
    private const string DeviceA = "pos-terminal-001";
    private const string DeviceB = "pos-terminal-002";

    private readonly IdentityHardeningTestDatabase _database;
    private readonly PostgresUserStore _store;
    private readonly DeviceSessionService _sessions;
    private readonly InMemorySuspiciousLoginAuditSink _sink = new();
    private readonly AccountRecoveryService _recovery;

    public AccountRecoveryServiceTests(IdentityHardeningTestDatabase database)
    {
        _database = database;
        _store = new PostgresUserStore(database.DataSource);
        _sessions = new DeviceSessionService(new PostgresDeviceSessionRepository(database.DataSource));
        _recovery = new AccountRecoveryService(_store, _sessions, _sink);
    }

    [Fact]
    public async Task RevokeAllSessionsInvalidatesEveryDeviceImmediately()
    {
        var userId = await _database.InsertUserAsync();
        var otherUserId = await _database.InsertUserAsync();
        var (_, tokenA) = await _sessions.CreateSessionAsync(userId, DeviceA);
        var (_, tokenB) = await _sessions.CreateSessionAsync(userId, DeviceB);
        var (otherSession, otherToken) = await _sessions.CreateSessionAsync(otherUserId, DeviceA);

        var revokedCount = await _recovery.RevokeAllSessionsAsync(userId, actor: "manager-1");

        revokedCount.Should().Be(2);
        await Assert.ThrowsAsync<DeviceSessionRevokedException>(
            () => _sessions.AuthenticateAsync(userId, DeviceA, tokenA));
        await Assert.ThrowsAsync<DeviceSessionRevokedException>(
            () => _sessions.AuthenticateAsync(userId, DeviceB, tokenB));

        // An unrelated user's session is untouched.
        var untouched = await _sessions.AuthenticateAsync(otherUserId, DeviceA, otherToken);
        untouched.SessionId.Should().Be(otherSession.SessionId);

        _sink.Events.Should().ContainSingle(e =>
            e.UserId == userId
            && e.Reason == SuspiciousLoginReason.AllSessionsRevoked
            && e.Actor == "manager-1");
    }

    [Fact]
    public async Task RevokeAllSessionsWithNoActiveSessionsReturnsZeroAndStillAudits()
    {
        var userId = await _database.InsertUserAsync();

        var revokedCount = await _recovery.RevokeAllSessionsAsync(userId, actor: "manager-1");

        revokedCount.Should().Be(0);
        _sink.Events.Should().ContainSingle(e => e.Reason == SuspiciousLoginReason.AllSessionsRevoked);
    }

    [Fact]
    public async Task ForceUnlockClearsALockBeforeItsWindowExpires()
    {
        var userId = await _database.InsertUserAsync(
            failedLoginAttempts: 5,
            lockedUntil: DateTimeOffset.UtcNow.AddMinutes(15));

        var unlocked = await _recovery.ForceUnlockAsync(userId, actor: "manager-1");

        unlocked.Should().BeTrue();
        var user = await _store.GetByIdAsync(userId);
        user!.LockedUntil.Should().BeNull();
        user.FailedLoginAttempts.Should().Be(0);

        _sink.Events.Should().ContainSingle(e =>
            e.UserId == userId
            && e.Reason == SuspiciousLoginReason.AccountForceUnlocked
            && e.Actor == "manager-1");
    }

    [Fact]
    public async Task DeactivateClosesTheAccountRevokesEverySessionAndAudits()
    {
        // V1-RMD-405: offboarding a leaver — no session survives and the account is closed for sign-in.
        var userId = await _database.InsertUserAsync();
        var (_, token) = await _sessions.CreateSessionAsync(userId, DeviceA);

        var deactivated = await _recovery.DeactivateAsync(userId, actor: "manager-1");

        deactivated.Should().BeTrue();
        (await _store.GetByIdAsync(userId))!.Active.Should().BeFalse();
        await Assert.ThrowsAsync<DeviceSessionRevokedException>(
            () => _sessions.AuthenticateAsync(userId, DeviceA, token));
        _sink.Events.Should().ContainSingle(e =>
            e.UserId == userId
            && e.Reason == SuspiciousLoginReason.AccountDeactivated
            && e.Actor == "manager-1");
    }

    [Fact]
    public async Task ReactivateReopensADeactivatedAccountAndAudits()
    {
        var userId = await _database.InsertUserAsync();
        await _recovery.DeactivateAsync(userId, actor: "manager-1");

        var reactivated = await _recovery.ReactivateAsync(userId, actor: "manager-2");

        reactivated.Should().BeTrue();
        (await _store.GetByIdAsync(userId))!.Active.Should().BeTrue();
        _sink.Events.Should().ContainSingle(e =>
            e.UserId == userId
            && e.Reason == SuspiciousLoginReason.AccountReactivated
            && e.Actor == "manager-2");
    }

    [Fact]
    public async Task DeactivateOrReactivateOnAnUnknownUserReturnsFalseAndDoesNotAudit()
    {
        (await _recovery.DeactivateAsync(Guid.NewGuid(), actor: "manager-1")).Should().BeFalse();
        (await _recovery.ReactivateAsync(Guid.NewGuid(), actor: "manager-1")).Should().BeFalse();
        _sink.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task ForceUnlockOnAnUnknownUserReturnsFalseAndDoesNotAudit()
    {
        var unlocked = await _recovery.ForceUnlockAsync(Guid.NewGuid(), actor: "manager-1");

        unlocked.Should().BeFalse();
        _sink.Events.Should().BeEmpty();
    }
}
