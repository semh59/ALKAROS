namespace ALKAROS.QrRelay.PublicGateway.Tests;

using ALKAROS.QrRelay.PublicGateway.Tests.Fixtures;
using Xunit;

/// <summary>
/// `relay_provider_config` holds a single well-known row — like
/// `V14-QRT-003`'s `relay_credentials`, each test here gets its own fresh
/// database (xUnit constructs a new instance of this class, and so a new
/// <see cref="RelayProviderConfigTestDatabase"/>, per [Fact]) rather than a
/// shared `IClassFixture`.
/// </summary>
public sealed class PostgresRelayProviderConfigStoreTests : IAsyncLifetime
{
    private readonly RelayProviderConfigTestDatabase _database = new();
    private PostgresRelayProviderConfigStore _store = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _store = new PostgresRelayProviderConfigStore(_database.DataSource);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task WithNothingSavedYetGetReturnsNull()
    {
        Assert.Null(await _store.GetAsync());
    }

    [Fact]
    public async Task SavingAndReadingRoundTripsAllFields()
    {
        await _store.SaveAsync("account-abc", "zone-xyz", "alkaros.app");

        var config = await _store.GetAsync();

        Assert.NotNull(config);
        Assert.Equal("account-abc", config!.AccountId);
        Assert.Equal("zone-xyz", config.ZoneId);
        Assert.Equal("alkaros.app", config.BaseDomain);
    }

    [Fact]
    public async Task SavingASecondTimeReplacesThePreviousConfig()
    {
        await _store.SaveAsync("account-1", "zone-1", "old.example");
        await _store.SaveAsync("account-2", "zone-2", "new.example");

        var config = await _store.GetAsync();

        Assert.Equal("account-2", config!.AccountId);
        Assert.Equal("new.example", config.BaseDomain);
    }
}
