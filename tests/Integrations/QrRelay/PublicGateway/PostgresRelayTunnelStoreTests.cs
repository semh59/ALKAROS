using ALKAROS.QrOrdering.RelayCredential;
using ALKAROS.QrRelay.PublicGateway.Tests.Fixtures;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Xunit;

namespace ALKAROS.QrRelay.PublicGateway.Tests;

/// <summary>
/// `relay_tunnel` holds a single well-known row — like
/// `PostgresRelayProviderConfigStoreTests` and `V12-QRT-003`'s
/// `relay_credentials` — so each test here gets its own fresh database
/// rather than a shared `IClassFixture`.
/// </summary>
public sealed class PostgresRelayTunnelStoreTests : IAsyncLifetime
{
    private readonly RelayProviderConfigTestDatabase _database = new();
    private PostgresRelayTunnelStore _store = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        var secretProvider = new InMemorySecretProvider();
        secretProvider.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator()));
        var accessPolicy = new RelayCredentialAccessPolicy();
        var resolver = new SecretResolver(secretProvider, accessPolicy);
        var cipher = new AesGcmEnvelopeCipher(resolver);
        var protector = new SensitivePayloadProtector(cipher, accessPolicy);
        _store = new PostgresRelayTunnelStore(_database.DataSource, protector);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    private static byte[] RandomNumberGenerator() =>
        System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);

    [Fact]
    public async Task WithNothingSavedYetGetInfoReturnsNull()
    {
        Assert.Null(await _store.GetInfoAsync());
        Assert.Null(await _store.ResolveTunnelTokenAsync());
    }

    [Fact]
    public async Task SavingAndReadingRoundTripsNonSecretInfo()
    {
        await _store.SaveAsync("tunnel-abc", "raw-run-token-value", "example.alkaros.app");

        var info = await _store.GetInfoAsync();

        Assert.NotNull(info);
        Assert.Equal("tunnel-abc", info!.TunnelId);
        Assert.Equal("example.alkaros.app", info.Hostname);
    }

    [Fact]
    public async Task TheTunnelTokenDecryptsBackToTheExactRawValue()
    {
        await _store.SaveAsync("tunnel-abc", "raw-run-token-value", "example.alkaros.app");

        Assert.Equal("raw-run-token-value", await _store.ResolveTunnelTokenAsync());
    }

    [Fact]
    public async Task SavingASecondTimeReplacesThePreviousTunnel()
    {
        await _store.SaveAsync("tunnel-1", "token-1", "old.alkaros.app");
        await _store.SaveAsync("tunnel-2", "token-2", "new.alkaros.app");

        var info = await _store.GetInfoAsync();

        Assert.Equal("tunnel-2", info!.TunnelId);
        Assert.Equal("new.alkaros.app", info.Hostname);
        Assert.Equal("token-2", await _store.ResolveTunnelTokenAsync());
    }
}
