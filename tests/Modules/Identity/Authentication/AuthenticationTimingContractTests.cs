using System.Globalization;
using ALKAROS.Identity.Authentication;
using Xunit;

namespace ALKAROS.Identity.Authentication.Tests;

/// <summary>
/// Deterministic proof of the login timing equality contract
/// (docs/engineering/login-timing-contract.md). No stopwatch and no real
/// PBKDF2 timing: an injected counting verifier proves the structural work
/// each login path performs.
/// </summary>
public sealed class AuthenticationTimingContractTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 4, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private sealed class FakeStore : IUserStore
    {
        public StoredUser? User { get; set; }

        public int GetCalls { get; private set; }
        public int FailureWrites { get; private set; }
        public int SuccessWrites { get; private set; }
        public List<(Guid UserId, DateTimeOffset Now, int MaxFailedAttempts, TimeSpan LockoutDuration)> FailureArgs { get; } = [];

        public Task<StoredUser?> GetByUsernameAsync(
            string username, CancellationToken cancellationToken = default)
        {
            GetCalls++;
            return Task.FromResult(User);
        }

        public Task<LoginFailureUpdate?> RecordLoginFailureAsync(
            Guid userId,
            DateTimeOffset now,
            int maxFailedAttempts,
            TimeSpan lockoutDuration,
            CancellationToken cancellationToken = default)
        {
            FailureWrites++;
            FailureArgs.Add((userId, now, maxFailedAttempts, lockoutDuration));
            return Task.FromResult<LoginFailureUpdate?>(new LoginFailureUpdate(1, null));
        }

        public Task<bool> RecordLoginSuccessAsync(
            Guid userId, DateTimeOffset lastLoginAt, CancellationToken cancellationToken = default)
        {
            SuccessWrites++;
            return Task.FromResult(true);
        }

        public Task<bool> TryUpgradePasswordHashAsync(
            Guid userId,
            string expectedCurrentHash,
            string upgradedHash,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private static StoredUser ActiveUser(string passwordHash) => new(
        UserId, "waiter1", passwordHash, "Test User", Active: true,
        FailedLoginAttempts: 0, LockedUntil: null, LastLoginAt: null);

    private static StoredUser InactiveUser(string passwordHash) => new(
        UserId, "fired", passwordHash, "Test User", Active: false,
        FailedLoginAttempts: 0, LockedUntil: null, LastLoginAt: null);

    private static StoredUser LockedUser(string passwordHash) => new(
        UserId, "lock-target", passwordHash, "Test User", Active: true,
        FailedLoginAttempts: 5, LockedUntil: Now.AddMinutes(15), LastLoginAt: null);

    private static (FakeStore Store, List<(string Password, string Hash)> VerifierCalls, AuthenticationService Service)
        CreateService(StoredUser? user, bool verifierResult)
    {
        var store = new FakeStore { User = user };
        var verifierCalls = new List<(string, string)>();
        PasswordVerifier verifier = (password, encodedHash) =>
        {
            verifierCalls.Add((password, encodedHash));
            return verifierResult;
        };
        var service = new AuthenticationService(store, verifier: verifier);
        return (store, verifierCalls, service);
    }

    [Fact]
    public async Task UnknownUsernamePerformsExactlyOneDummyVerificationAndNoWrites()
    {
        var (store, calls, service) = CreateService(user: null, verifierResult: true);

        var result = await service.LoginAsync("ghost-user", "some-password", Now);

        Assert.Equal(LoginFailureReason.InvalidCredentials, Assert.IsType<LoginFailure>(result).Reason);
        Assert.Single(calls);
        Assert.Equal(("some-password", PasswordHasher.DummyHash), calls[0]);
        Assert.Equal(1, store.GetCalls);
        Assert.Equal(0, store.FailureWrites);
        Assert.Equal(0, store.SuccessWrites);
    }

    [Fact]
    public async Task InactiveUserPerformsExactlyOneDummyVerificationAndNoWrites()
    {
        var (store, calls, service) = CreateService(
            InactiveUser("pbkdf2-sha256$600000$not-checked"), verifierResult: true);

        var result = await service.LoginAsync("fired", "some-password", Now);

        Assert.Equal(LoginFailureReason.InvalidCredentials, Assert.IsType<LoginFailure>(result).Reason);
        Assert.Single(calls);
        Assert.Equal(("some-password", PasswordHasher.DummyHash), calls[0]);
        Assert.Equal(0, store.FailureWrites);
        Assert.Equal(0, store.SuccessWrites);
    }

    [Fact]
    public async Task WrongPasswordPerformsExactlyOneVerificationAndOneFailureWrite()
    {
        const string storedHash = "pbkdf2-sha256$600000$stored-hash";
        var (store, calls, service) = CreateService(ActiveUser(storedHash), verifierResult: false);

        var result = await service.LoginAsync("waiter1", "wrong", Now);

        Assert.Equal(LoginFailureReason.InvalidCredentials, Assert.IsType<LoginFailure>(result).Reason);
        Assert.Single(calls);
        Assert.Equal(("wrong", storedHash), calls[0]);
        Assert.Equal(1, store.FailureWrites);
        Assert.Equal(0, store.SuccessWrites);
        Assert.Single(store.FailureArgs);
        Assert.Equal(
            (UserId, Now, AuthenticationService.DefaultMaxFailedAttempts, AuthenticationService.DefaultLockoutDuration),
            store.FailureArgs[0]);
    }

    [Fact]
    public async Task LockedOutPerformsZeroVerificationsAndZeroWrites()
    {
        var (store, calls, service) = CreateService(
            LockedUser("pbkdf2-sha256$600000$stored-hash"), verifierResult: true);

        var result = await service.LoginAsync("lock-target", "correct", Now);

        Assert.Equal(LoginFailureReason.LockedOut, Assert.IsType<LoginFailure>(result).Reason);
        Assert.Empty(calls);
        Assert.Equal(0, store.FailureWrites);
        Assert.Equal(0, store.SuccessWrites);
    }

    [Fact]
    public async Task SuccessfulLoginPerformsExactlyOneVerificationAndOneSuccessWrite()
    {
        const string storedHash = "pbkdf2-sha256$600000$stored-hash";
        var (store, calls, service) = CreateService(ActiveUser(storedHash), verifierResult: true);

        var result = await service.LoginAsync("waiter1", "correct", Now);

        var success = Assert.IsType<LoginSuccess>(result);
        Assert.Equal(UserId, success.UserId);
        Assert.Single(calls);
        Assert.Equal(("correct", storedHash), calls[0]);
        Assert.Equal(0, store.FailureWrites);
        Assert.Equal(1, store.SuccessWrites);
    }

    [Fact]
    public void DummyHashIsAuthenticAndEmbedsDefaultIterations()
    {
        var embeddedIterations = int.Parse(
            PasswordHasher.DummyHash.Split('$')[1], CultureInfo.InvariantCulture);
        Assert.Equal(PasswordHasher.DefaultIterations, embeddedIterations);
        Assert.True(PasswordHasher.Verify(PasswordHasher.DummyPassword, PasswordHasher.DummyHash));
    }

    [Theory]
    [InlineData("pbkdf2-sha256$999$QUJD$QUJD")]                       // below MinimumIterations
    [InlineData("pbkdf2-sha256$2000001$QUJD$QUJD")]                   // above MaximumIterations
    [InlineData("pbkdf2-sha256$600000$QUJD")]                         // missing hash segment
    [InlineData("pbkdf2-sha256$600000$not-base64!$not-base64!")]      // invalid base64
    [InlineData("pbkdf2-sha256$600000$QUJD$QQ==")]                    // wrong hash size
    [InlineData("md5$600000$QUJD$QUJD")]                              // unknown algorithm tag
    public void StoredHashIterationAndFormatBoundsAreEnforced(string encodedHash)
    {
        Assert.False(PasswordHasher.Verify("any-password", encodedHash));
    }
}
