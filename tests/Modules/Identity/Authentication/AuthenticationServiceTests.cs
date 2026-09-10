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
    public async Task SuccessfulLoginUpgradesLegacyPasswordHashToCurrentWorkFactor()
    {
        var legacyHash = new PasswordHasher(iterations: 10_000).Hash("legacy-secret");
        var userId = await _database.InsertUserAsync("legacy-hash", legacyHash);

        var result = await _service.LoginAsync("legacy-hash", "legacy-secret", Now);

        Assert.IsType<LoginSuccess>(result);
        var upgradedHash = await _database.ScalarAsync<string>(
            "SELECT password_hash FROM identity.users WHERE user_id = '" + userId + "';");
        Assert.Equal(PasswordHasher.DefaultIterations,
            int.Parse(upgradedHash.Split('$')[1], System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(PasswordHasher.Verify("legacy-secret", upgradedHash));
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

    [Fact]
    public async Task ExpiredLockRestartsFailureCountingAndReLocksOnlyAfterNewMaxFailures()
    {
        var userId = await _database.InsertUserAsync("expired-restart", _hasher.Hash("correct"));
        var service = new AuthenticationService(_store, maxFailedAttempts: 2);

        await service.LoginAsync("expired-restart", "wrong-1", Now);
        await service.LoginAsync("expired-restart", "wrong-2", Now);
        Assert.Equal(LoginFailureReason.LockedOut,
            Assert.IsType<LoginFailure>(await service.LoginAsync("expired-restart", "wrong-3", Now)).Reason);

        await _database.ForceLockExpiredAsync(userId, Now.AddSeconds(-1));
        var afterExpiry = Now.AddMinutes(16);

        var firstAfter = await service.LoginAsync("expired-restart", "wrong-4", afterExpiry);
        Assert.Equal(LoginFailureReason.InvalidCredentials, Assert.IsType<LoginFailure>(firstAfter).Reason);
        Assert.Equal(1, await _database.ScalarAsync<int>(
            "SELECT failed_login_attempts FROM identity.users WHERE user_id = '" + userId + "';"));
        Assert.False(await _database.ScalarAsync<bool>(
            "SELECT locked_until IS NOT NULL FROM identity.users WHERE user_id = '" + userId + "';"));

        var secondAfter = await service.LoginAsync("expired-restart", "wrong-5", afterExpiry);
        Assert.Equal(LoginFailureReason.InvalidCredentials, Assert.IsType<LoginFailure>(secondAfter).Reason);
        Assert.Equal(2, await _database.ScalarAsync<int>(
            "SELECT failed_login_attempts FROM identity.users WHERE user_id = '" + userId + "';"));
        Assert.True(await _database.ScalarAsync<bool>(
            "SELECT locked_until IS NOT NULL FROM identity.users WHERE user_id = '" + userId + "';"));
    }

    [Fact]
    public async Task ConcurrentWrongPasswordsDoNotLoseAttemptsAndArmLockout()
    {
        var userId = await _database.InsertUserAsync("concurrent-login", _hasher.Hash("correct"));
        var service = new AuthenticationService(_store, maxFailedAttempts: 5);

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
            service.LoginAsync("concurrent-login", "wrong", Now)));

        Assert.All(results, result => Assert.IsType<LoginFailure>(result));
        Assert.Equal(5, await _database.ScalarAsync<int>(
            "SELECT failed_login_attempts FROM identity.users WHERE user_id = '" + userId + "';"));
        Assert.True(await _database.ScalarAsync<bool>(
            "SELECT locked_until IS NOT NULL FROM identity.users WHERE user_id = '" + userId + "';"));
    }

    // V1-RMD-151: a device session lives 30 days and nothing asked who was
    // holding the device. These cover the PIN that now guards an idle one.

    [Fact]
    public async Task ACorrectPinUnlocksAndClearsTheAttemptCounter()
    {
        var userId = await _database.InsertUserAsync("pin-ok", _hasher.Hash("s3cret-Pass"));
        var service = new AuthenticationService(_store);
        Assert.True(await service.SetPinAsync(userId, "s3cret-Pass", "1234"));

        Assert.IsType<UnlockFailure>(await service.UnlockAsync(userId, "9999", Now));
        var result = await service.UnlockAsync(userId, "1234", Now);

        Assert.IsType<UnlockSuccess>(result);
        Assert.Equal(0, await _database.ScalarAsync<int>(
            "SELECT pin_failed_attempts FROM identity.users WHERE user_id = '" + userId + "';"));
    }

    [Fact]
    public async Task FiveWrongPinsLockThePinButLeaveTheAccountSignInWorking()
    {
        // The whole point of separate counters: a forgotten PIN must not stop
        // the waiter from working, and PIN guesses must not lock the account.
        var userId = await _database.InsertUserAsync("pin-lock", _hasher.Hash("s3cret-Pass"));
        var service = new AuthenticationService(_store, maxFailedAttempts: 5);
        Assert.True(await service.SetPinAsync(userId, "s3cret-Pass", "1234"));

        for (var attempt = 0; attempt < 5; attempt++)
            Assert.IsType<UnlockFailure>(await service.UnlockAsync(userId, "0000", Now));

        var locked = Assert.IsType<UnlockFailure>(await service.UnlockAsync(userId, "1234", Now));
        Assert.Equal(UnlockFailureReason.LockedOut, locked.Reason);

        // The password path is untouched.
        Assert.Equal(0, await _database.ScalarAsync<int>(
            "SELECT failed_login_attempts FROM identity.users WHERE user_id = '" + userId + "';"));
        Assert.IsType<LoginSuccess>(await service.LoginAsync("pin-lock", "s3cret-Pass", Now));
    }

    [Fact]
    public async Task UnlockingIsRefusedWhenNoPinWasEverSet()
    {
        var userId = await _database.InsertUserAsync("pin-none", _hasher.Hash("s3cret-Pass"));
        var service = new AuthenticationService(_store);

        var result = Assert.IsType<UnlockFailure>(await service.UnlockAsync(userId, "1234", Now));

        Assert.Equal(UnlockFailureReason.PinNotSet, result.Reason);
    }

    [Fact]
    public async Task SettingAPinRequiresTheCurrentPassword()
    {
        // A device left unlocked on a table must not be enough to plant a PIN.
        var userId = await _database.InsertUserAsync("pin-guard", _hasher.Hash("s3cret-Pass"));
        var service = new AuthenticationService(_store);

        Assert.False(await service.SetPinAsync(userId, "wrong-password", "1234"));
        Assert.IsType<UnlockFailure>(await service.UnlockAsync(userId, "1234", Now));
    }

    [Fact]
    public async Task ClearingThePinTurnsUnlockingOffAgain()
    {
        // Semih's decision (2026-09-10): the PIN is optional and removable.
        var userId = await _database.InsertUserAsync("pin-clear", _hasher.Hash("s3cret-Pass"));
        var service = new AuthenticationService(_store);
        Assert.True(await service.SetPinAsync(userId, "s3cret-Pass", "1234"));
        Assert.True(await service.SetPinAsync(userId, "s3cret-Pass", null));

        var result = Assert.IsType<UnlockFailure>(await service.UnlockAsync(userId, "1234", Now));

        Assert.Equal(UnlockFailureReason.PinNotSet, result.Reason);
    }
}
