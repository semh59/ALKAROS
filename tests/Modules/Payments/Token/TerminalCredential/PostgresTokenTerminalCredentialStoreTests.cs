namespace ALKAROS.Payments.Token.TerminalCredential.Tests;

using System.Security.Cryptography;
using System.Text;
using ALKAROS.Payments.Token.TerminalCredential.Tests.Fixtures;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Xunit;

/// <summary>
/// V13-HUG-005, against a real Postgres database. Mirrors
/// `ALKAROS.QrOrdering.RelayCredential.Tests.PostgresRelayCredentialStoreTests`'s
/// shape, but `PostgresTokenTerminalCredentialStore` takes `ISecretProvider`
/// (not a pre-built `SensitivePayloadProtector`) — see
/// `TokenTerminalCredentialAccessPolicy`'s doc comment for why: this
/// module deliberately never touches the shared `ISensitiveDataAccessPolicy`/
/// `SensitivePayloadProtector` DI registration `QrOrderingModule` already
/// owns, to avoid two modules' accessor policies silently overriding each
/// other. `token_terminal_credentials` holds a single well-known row, so
/// each test gets its own fresh database, same as the Relay tests.
/// </summary>
public sealed class PostgresTokenTerminalCredentialStoreTests : IAsyncLifetime
{
    private readonly TokenTerminalCredentialTestDatabase _database = new();
    private InMemorySecretProvider _secretProvider = null!;
    private PostgresTokenTerminalCredentialStore _store = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _secretProvider = new InMemorySecretProvider();
        _secretProvider.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        _store = new PostgresTokenTerminalCredentialStore(_database.DataSource, _secretProvider);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    private static SaveTokenTerminalCredentialRequest SampleRequest(string clientSecret = "cs-super-secret-abc123") =>
        new(MerchantId: "13e5862b-1328-47dd-887c-d9ca6cb4375c", BranchId: "b81bb869-d45c-43df-a078-9337900ff84e",
            TerminalId: "AV0000111044", ClientId: "cid-example", ClientSecret: clientSecret);

    [Fact]
    public async Task WithNothingSavedYetStatusIsNotConfigured()
    {
        var status = await _store.GetStatusAsync();
        Assert.False(status.Configured);
        Assert.Null(status.UpdatedAt);
        Assert.Null(status.MerchantId);

        var resolved = await _store.ResolveClientSecretAsync();
        Assert.Null(resolved);
    }

    [Fact]
    public async Task SavingAndResolvingRoundTripsEveryField()
    {
        await _store.SaveAsync(SampleRequest(), Guid.NewGuid());

        var resolvedSecret = await _store.ResolveClientSecretAsync();
        Assert.Equal("cs-super-secret-abc123", resolvedSecret);

        var status = await _store.GetStatusAsync();
        Assert.True(status.Configured);
        Assert.NotNull(status.UpdatedAt);
        Assert.Equal("13e5862b-1328-47dd-887c-d9ca6cb4375c", status.MerchantId);
        Assert.Equal("b81bb869-d45c-43df-a078-9337900ff84e", status.BranchId);
        Assert.Equal("AV0000111044", status.TerminalId);
        Assert.Equal("cid-example", status.ClientId);
    }

    [Fact]
    public async Task TheStatusNeverExposesTheClientSecret()
    {
        await _store.SaveAsync(SampleRequest(), Guid.NewGuid());

        var status = await _store.GetStatusAsync();
        // TokenTerminalCredentialStatus has no ClientSecret property at
        // all (compile-time guarantee) — this asserts the runtime side:
        // the secret does not leak into any of the fields it DOES expose.
        Assert.DoesNotContain("cs-super-secret-abc123", status.MerchantId ?? "");
        Assert.DoesNotContain("cs-super-secret-abc123", status.ClientId ?? "");
    }

    [Fact]
    public async Task TheDatabaseNeverStoresTheClientSecretInPlaintext()
    {
        const string secret = "cs-super-secret-xyz";
        await _store.SaveAsync(SampleRequest(secret), Guid.NewGuid());

        var envelopeBytes = await _database.RawEnvelopeBytesAsync();
        var asLatin1 = Encoding.Latin1.GetString(envelopeBytes);
        Assert.DoesNotContain(secret, asLatin1, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SavingASecondCredentialReplacesTheFirstOneEntirely()
    {
        await _store.SaveAsync(SampleRequest("first-secret"), Guid.NewGuid());
        await _store.SaveAsync(
            new SaveTokenTerminalCredentialRequest("merchant-2", "branch-2", "AV0000222222", "cid-2", "second-secret"),
            Guid.NewGuid());

        var resolved = await _store.ResolveClientSecretAsync();
        Assert.Equal("second-secret", resolved);

        var status = await _store.GetStatusAsync();
        Assert.Equal("AV0000222222", status.TerminalId);
        Assert.Equal("cid-2", status.ClientId);
    }

    [Fact]
    public async Task ResolvingFailsClosedRatherThanReturningGarbageWhenTheMasterKeyChanged()
    {
        await _store.SaveAsync(SampleRequest(), Guid.NewGuid());

        // Simulates the environment variable holding the master key being
        // rotated/lost between save and resolve — the store must surface a
        // typed failure, never silently decrypt to garbage bytes.
        _secretProvider.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        await Assert.ThrowsAsync<SensitiveDataEncryptionException>(() => _store.ResolveClientSecretAsync());
    }
}
