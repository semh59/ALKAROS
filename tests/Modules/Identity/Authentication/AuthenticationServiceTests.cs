using ALKAROS.Identity.Authentication;
using ALKAROS.Identity.Authentication.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Identity.Authentication.Tests;

public sealed class AuthenticationServiceTests : IClassFixture<AuthTestDatabase>
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly AuthTestDatabase _database;
    private readonly PostgresUserStore _store;
    private readonly PasswordHasher _hasher;
    private readonly AuthenticationService _service;

    public AuthenticationServiceTests(AuthTestDatabase database)
    {
        _database = database;
        _store = new PostgresUserStore(database.DataSource);
        _hasher = new PasswordHasher();
        _service = new AuthenticationService(_store);
    }

    [Fact]
    public async Task ValidCredentialsLoginSucceedsAndIssuesSession()
    {
        var userId = await _database.InsertUserAsync("waiter1", _hasher.Hash("s3cret-Pass"));

        var result = await _service.LoginAsync("waiter1", "s3cret-Pass", Now);

        var success = Assert.IsType<LoginSuccess>(result);
        Assert.Equal(userId, success.UserId);
        Assert.Equal("Test User", success.DisplayName);
        Assert.Equal(44, success.Session.Token.Length);
        Assert.Equal(64, success.Session.TokenHash.Length);
        Assert.Equal(Now + SessionTokenIssuer.DefaultLifetime, success.Session.ExpiresAt);
    }

    [Fact]
    public async Task SuccessfulLoginClearsFailureCounterAndSetsLastLogin()
    {
        var userId = await _database.InsertUserAsync(
            "retry-user", _hasher.Hash("correct"), failedLoginAttempts: 3);

        var result = await _service.LoginAsync("retry-user", "correct", Now);

        Assert.IsType<LoginSuccess>(result);
        Assert.Equal(0, await _database.ScalarAsync<int>(
            "SELECT failed_login_attempts FROM identity.users WHERE user_id = '" + userId + "';"));
        Assert.True(await _database.ScalarAsync<bool>(
            "SELECT last_login_at IS NOT NULL FROM identity.users WHERE user_id = '" + userId + "';"));
    }

    [Fact]
    public async Task UnknownUsernameFailsWithoutLeakingUserExistence()
    {
        var result = await _service.LoginAsync("nobody", "whatever", Now);

        var failure = Assert.IsType<LoginFailure>(result);
        Assert.Equal(LoginFailureReason.InvalidCredentials, failure.Reason);
    }

    [Fact]
    public async Task WrongPasswordFailsWithoutLeakingCredentials()
    {
        await _database.InsertUserAsync("waiter2", _hasher.Hash("correct"));

        var result = await _service.LoginAsync("waiter2", "wrong", Now);

        var failure = Assert.IsType<LoginFailure>(result);
        Assert.Equal(LoginFailureReason.InvalidCredentials, failure.Reason);
    }

    [Fact]
    public async Task InactiveUserIsRejectedWithInvalidCredentials()
    {
        await _database.InsertUserAsync("fired", _hasher.Hash("correct"), active: false);

        var result = await _service.LoginAsync("fired", "correct", Now);

        var failure = Assert.IsType<LoginFailure>(result);
        Assert.Equal(LoginFailureReason.InvalidCredentials, failure.Reason);
    }

    [Fact]
    public async Task RepeatedFailuresIncrementCounterAndPersistIt()
    {
        var userId = await _database.InsertUserAsync("counter", _hasher.Hash("correct"));

        await _service.LoginAsync("counter", "wrong-1", Now);
        await _service.LoginAsync("counter", "wrong-2", Now);

        var attempts = await _database.ScalarAsync<int>(
            "SELECT failed_login_attempts FROM identity.users WHERE user_id = '" + userId + "';");
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task LockoutArmsAfterMaxFailuresAndRejectsEvenTheCorrectPassword()
    {
        var userId = await _database.InsertUserAsync("lock-target", _hasher.Hash("correct"));
        var service = new AuthenticationService(_store, maxFailedAttempts: 3);

        await service.LoginAsync("lock-target", "wrong-1", Now);
        await service.LoginAsync("lock-target", "wrong-2", Now);
        var third = await service.LoginAsync("lock-target", "wrong-3", Now);
        var locked = await service.LoginAsync("lock-target", "correct", Now);

        Assert.Equal(LoginFailureReason.InvalidCredentials, Assert.IsType<LoginFailure>(third).Reason);
        var lockedFailure = Assert.IsType<LoginFailure>(locked);
        Assert.Equal(LoginFailureReason.LockedOut, lockedFailure.Reason);
        Assert.True(await _database.ScalarAsync<bool>(
            "SELECT locked_until IS NOT NULL FROM identity.users WHERE user_id = '" + userId + "';"));
    }

    [Fact]
    public async Task LockExpiryAllowsLoginAgain()
    {
        var userId = await _database.InsertUserAsync("lock-expiry", _hasher.Hash("correct"));
        var service = new AuthenticationService(_store, maxFailedAttempts: 2);

        await service.LoginAsync("lock-expiry", "wrong-1", Now);
        await service.LoginAsync("lock-expiry", "wrong-2", Now);
        Assert.Equal(LoginFailureReason.LockedOut,
            Assert.IsType<LoginFailure>(await service.LoginAsync("lock-expiry", "correct", Now)).Reason);

        await _database.ForceLockExpiredAsync(userId, Now.AddSeconds(-1));
        var after = await service.LoginAsync("lock-expiry", "correct", Now.AddMinutes(16));

        Assert.IsType<LoginSuccess>(after);
    }

    [Fact]
    public async Task LockedOutAttemptDoesNotIncrementFailureCounter()
    {
        var userId = await _database.InsertUserAsync("lock-counter", _hasher.Hash("correct"));
        var service = new AuthenticationService(_store, maxFailedAttempts: 2);

        await service.LoginAsync("lock-counter", "wrong-1", Now);
        await service.LoginAsync("lock-counter", "wrong-2", Now);
        await service.LoginAsync("lock-counter", "wrong-3", Now);

        var attempts = await _database.ScalarAsync<int>(
            "SELECT failed_login_attempts FROM identity.users WHERE user_id = '" + userId + "';");
        Assert.Equal(2, attempts);
    }
}
