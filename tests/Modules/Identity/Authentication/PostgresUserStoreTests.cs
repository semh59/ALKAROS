using ALKAROS.Identity.Authentication;
using ALKAROS.Identity.Authentication.Tests.Fixtures;
using Xunit;

namespace ALKAROS.Identity.Authentication.Tests;

public sealed class PostgresUserStoreTests : IClassFixture<AuthTestDatabase>
{
    private readonly AuthTestDatabase _database;
    private readonly PostgresUserStore _store;

    public PostgresUserStoreTests(AuthTestDatabase database)
    {
        _database = database;
        _store = new PostgresUserStore(database.DataSource);
    }

    [Fact]
    public async Task GetByUsernameReturnsTheStoredUser()
    {
        var userId = await _database.InsertUserAsync("store-user", "hash-value");

        var user = await _store.GetByUsernameAsync("store-user");

        Assert.NotNull(user);
        Assert.Equal(userId, user.UserId);
        Assert.Equal("store-user", user.Username);
        Assert.Equal("hash-value", user.PasswordHash);
        Assert.Equal("Test User", user.DisplayName);
        Assert.True(user.Active);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockedUntil);
        Assert.Null(user.LastLoginAt);
    }

    [Fact]
    public async Task GetByUsernameReturnsNullForUnknownUser()
    {
        Assert.Null(await _store.GetByUsernameAsync("missing-user"));
    }

    [Fact]
    public async Task GetByUsernameIsCaseSensitive()
    {
        await _database.InsertUserAsync("CaseSensitive", "hash-value");

        Assert.NotNull(await _store.GetByUsernameAsync("CaseSensitive"));
        Assert.Null(await _store.GetByUsernameAsync("casesensitive"));
    }

    [Fact]
    public async Task RecordLoginFailurePersistsAttemptsAndLock()
    {
        var userId = await _database.InsertUserAsync("fail-store", "hash-value");
        var lockedUntil = new DateTimeOffset(2026, 8, 2, 12, 30, 0, TimeSpan.Zero);

        await _store.RecordLoginFailureAsync(userId, failedLoginAttempts: 4, lockedUntil);

        var user = await _store.GetByUsernameAsync("fail-store");
        Assert.NotNull(user);
        Assert.Equal(4, user.FailedLoginAttempts);
        Assert.Equal(lockedUntil, user.LockedUntil);
    }

    [Fact]
    public async Task RecordLoginSuccessClearsFailuresAndSetsLastLogin()
    {
        var userId = await _database.InsertUserAsync(
            "success-store", "hash-value", failedLoginAttempts: 3,
            lockedUntil: new DateTimeOffset(2026, 8, 2, 12, 30, 0, TimeSpan.Zero));
        var lastLoginAt = new DateTimeOffset(2026, 8, 2, 12, 15, 0, TimeSpan.Zero);

        await _store.RecordLoginSuccessAsync(userId, lastLoginAt);

        var user = await _store.GetByUsernameAsync("success-store");
        Assert.NotNull(user);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockedUntil);
        Assert.Equal(lastLoginAt, user.LastLoginAt);
    }

    [Fact]
    public async Task RecordLoginSuccessBumpsRowVersion()
    {
        var userId = await _database.InsertUserAsync("version-store", "hash-value");

        await _store.RecordLoginSuccessAsync(
            userId, new DateTimeOffset(2026, 8, 2, 12, 15, 0, TimeSpan.Zero));

        Assert.Equal(1, await _database.ScalarAsync<long>(
            "SELECT row_version FROM identity.users WHERE user_id = '" + userId + "';"));
    }

    [Fact]
    public async Task RecordOperationsOnMissingUserThrow()
    {
        var missing = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.RecordLoginFailureAsync(missing, 1, null));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.RecordLoginSuccessAsync(missing, DateTimeOffset.UtcNow));
    }
}
