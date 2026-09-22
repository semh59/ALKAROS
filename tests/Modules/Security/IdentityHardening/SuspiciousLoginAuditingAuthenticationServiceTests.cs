using ALKAROS.Identity.Authentication;
using ALKAROS.Security.IdentityHardening.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Security.IdentityHardening.Tests;

public sealed class SuspiciousLoginAuditingAuthenticationServiceTests : IClassFixture<IdentityHardeningTestDatabase>
{
    private const string CorrectPassword = "correct-password";
    private const string MatchingHash = "matching-hash";

    // A lightweight fake verifier (same shape AuthenticationTimingContractTests
    // uses) — this decorator's own behaviour is under test, not PBKDF2.
    private static bool Verify(string password, string encodedHash)
        => password == CorrectPassword && encodedHash == MatchingHash;

    private readonly IdentityHardeningTestDatabase _database;
    private readonly PostgresUserStore _store;
    private readonly InMemorySuspiciousLoginAuditSink _sink = new();
    private readonly SuspiciousLoginAuditingAuthenticationService _auditing;

    public SuspiciousLoginAuditingAuthenticationServiceTests(IdentityHardeningTestDatabase database)
    {
        _database = database;
        _store = new PostgresUserStore(database.DataSource);
        var inner = new AuthenticationService(_store, verifier: Verify);
        _auditing = new SuspiciousLoginAuditingAuthenticationService(inner, _store, _sink);
    }

    private async Task<Guid> InsertUserAsync(int failedLoginAttempts = 0, DateTimeOffset? lockedUntil = null)
    {
        var userId = Guid.NewGuid();
        await _database.ExecuteAsync(
            """
            INSERT INTO identity.users
                (user_id, username, password_hash, display_name, active,
                 failed_login_attempts, locked_until)
            VALUES
                (@user_id, @username, @password_hash, @display_name, true,
                 @failed_login_attempts, @locked_until);
            """,
            ("user_id", userId),
            ("username", "user_" + userId.ToString("N")[..20]),
            ("password_hash", MatchingHash),
            ("display_name", "Test User"),
            ("failed_login_attempts", failedLoginAttempts),
            ("locked_until", (object?)lockedUntil ?? DBNull.Value));

        return await Task.FromResult(userId);
    }

    private async Task<string> UsernameOfAsync(Guid userId)
        => (await _store.GetByIdAsync(userId))!.Username;

    [Fact]
    public async Task SuccessWithNoPriorFailuresIsNotAudited()
    {
        var userId = await InsertUserAsync(failedLoginAttempts: 0);
        var username = await UsernameOfAsync(userId);

        var result = await _auditing.LoginAsync(username, CorrectPassword, DateTimeOffset.UtcNow);

        result.Should().BeOfType<LoginSuccess>();
        _sink.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task SuccessAfterRepeatedFailuresIsAudited()
    {
        var userId = await InsertUserAsync(failedLoginAttempts: 3);
        var username = await UsernameOfAsync(userId);

        var result = await _auditing.LoginAsync(username, CorrectPassword, DateTimeOffset.UtcNow);

        result.Should().BeOfType<LoginSuccess>();
        _sink.Events.Should().ContainSingle(e =>
            e.UserId == userId
            && e.Reason == SuspiciousLoginReason.SuccessAfterRepeatedFailures
            && e.PriorFailedAttempts == 3);
    }

    [Fact]
    public async Task LockedOutAttemptIsAudited()
    {
        var userId = await InsertUserAsync(
            failedLoginAttempts: 5,
            lockedUntil: DateTimeOffset.UtcNow.AddMinutes(15));
        var username = await UsernameOfAsync(userId);

        var result = await _auditing.LoginAsync(username, CorrectPassword, DateTimeOffset.UtcNow);

        result.Should().BeOfType<LoginFailure>()
            .Which.Reason.Should().Be(LoginFailureReason.LockedOut);
        _sink.Events.Should().ContainSingle(e =>
            e.UserId == userId && e.Reason == SuspiciousLoginReason.LockoutTriggered);
    }

    [Fact]
    public async Task FailureBelowTheThresholdIsNotAudited()
    {
        var userId = await InsertUserAsync(failedLoginAttempts: 1);
        var username = await UsernameOfAsync(userId);

        var result = await _auditing.LoginAsync(username, "wrong-password", DateTimeOffset.UtcNow);

        result.Should().BeOfType<LoginFailure>()
            .Which.Reason.Should().Be(LoginFailureReason.InvalidCredentials);
        _sink.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task LockoutMechanicsAreUnchangedAndOtherAccountsStayUnaffected()
    {
        // Decorator must never widen or narrow who gets locked — it only
        // observes the inner service's own decision.
        var target = await InsertUserAsync(failedLoginAttempts: 4);
        var innocent = await InsertUserAsync(failedLoginAttempts: 0);
        var targetUsername = await UsernameOfAsync(target);
        var innocentUsername = await UsernameOfAsync(innocent);

        // 5th failure arms the lock (AuthenticationService.DefaultMaxFailedAttempts).
        await _auditing.LoginAsync(targetUsername, "wrong-password", DateTimeOffset.UtcNow);

        var targetUser = await _store.GetByIdAsync(target);
        targetUser!.LockedUntil.Should().NotBeNull();

        var innocentUser = await _store.GetByIdAsync(innocent);
        innocentUser!.LockedUntil.Should().BeNull();
        innocentUser.FailedLoginAttempts.Should().Be(0);

        // The innocent account can still log in normally through the decorator.
        var result = await _auditing.LoginAsync(innocentUsername, CorrectPassword, DateTimeOffset.UtcNow);
        result.Should().BeOfType<LoginSuccess>();
    }
}
