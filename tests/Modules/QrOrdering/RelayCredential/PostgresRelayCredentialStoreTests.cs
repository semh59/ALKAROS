namespace ALKAROS.QrOrdering.RelayCredential.Tests;

using System.Security.Cryptography;
using System.Text;
using ALKAROS.QrOrdering.RelayCredential.Tests.Fixtures;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Xunit;

/// <summary>
/// V14-QRT-003, against a real Postgres database. `relay_credentials` holds
/// a single well-known row, so — unlike the table-token tests — each test
/// here gets its own fresh database (xUnit constructs a new instance of
/// this class, and so a new <see cref="RelayCredentialTestDatabase"/>, per
/// [Fact]) rather than a shared <c>IClassFixture</c>.
/// </summary>
public sealed class PostgresRelayCredentialStoreTests : IAsyncLifetime
{
    private readonly RelayCredentialTestDatabase _database = new();
    private PostgresRelayCredentialStore _store = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        var secretProvider = new InMemorySecretProvider();
        secretProvider.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var accessPolicy = new RelayCredentialAccessPolicy();
        var resolver = new SecretResolver(secretProvider, accessPolicy);
        var cipher = new AesGcmEnvelopeCipher(resolver);
        var protector = new SensitivePayloadProtector(cipher, accessPolicy);
        _store = new PostgresRelayCredentialStore(_database.DataSource, protector);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task WithNothingSavedYetStatusIsNotConfigured()
    {
        var status = await _store.GetStatusAsync();
        Assert.False(status.Configured);
        Assert.Null(status.UpdatedAt);

        var resolved = await _store.ResolveCloudflareApiTokenAsync();
        Assert.Null(resolved);
    }

    [Fact]
    public async Task SavingAndResolvingRoundTripsTheRawToken()
    {
        await _store.SaveCloudflareApiTokenAsync("cf-test-token-abc123", Guid.NewGuid());

        var resolved = await _store.ResolveCloudflareApiTokenAsync();
        Assert.Equal("cf-test-token-abc123", resolved);

        var status = await _store.GetStatusAsync();
        Assert.True(status.Configured);
        Assert.NotNull(status.UpdatedAt);
    }

    [Fact]
    public async Task TheDatabaseNeverStoresTheRawTokenInPlaintext()
    {
        const string raw = "cf-super-secret-token-xyz";
        await _store.SaveCloudflareApiTokenAsync(raw, Guid.NewGuid());

        var envelopeBytes = await _database.RawEnvelopeBytesAsync();
        // The envelope's JSON metadata is plaintext (classification, nonce,
        // tag) but the raw token itself must appear nowhere in those bytes.
        var asLatin1 = Encoding.Latin1.GetString(envelopeBytes);
        Assert.DoesNotContain(raw, asLatin1, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SavingASecondTokenReplacesTheFirstOne()
    {
        await _store.SaveCloudflareApiTokenAsync("first-token", Guid.NewGuid());
        await _store.SaveCloudflareApiTokenAsync("second-token", Guid.NewGuid());

        var resolved = await _store.ResolveCloudflareApiTokenAsync();
        Assert.Equal("second-token", resolved);
    }

    [Fact]
    public async Task WhenTheAccessPolicyDeniesReadingDecryptionIsRefusedBeforeTheCipherRuns()
    {
        await _store.SaveCloudflareApiTokenAsync("cf-test-token", Guid.NewGuid());

        var secretProvider = new InMemorySecretProvider();
        secretProvider.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        var denyAllPolicy = new DenyAllAccessPolicy();
        var resolver = new SecretResolver(secretProvider, denyAllPolicy);
        var cipher = new AesGcmEnvelopeCipher(resolver);
        var protectorUnderDenyAllPolicy = new SensitivePayloadProtector(cipher, denyAllPolicy);
        var storeUnderDenyAllPolicy = new PostgresRelayCredentialStore(_database.DataSource, protectorUnderDenyAllPolicy);

        // SensitivePayloadProtector.Unprotect checks CanRead before it ever
        // calls the cipher (SensitivePayloadProtector.cs) — this denies the
        // read even though the two provider instances hold different,
        // otherwise-unrelated master keys, which alone would already make
        // decryption fail differently (an integrity error, not this one).
        await Assert.ThrowsAsync<UnauthorizedSensitiveReadException>(
            () => storeUnderDenyAllPolicy.ResolveCloudflareApiTokenAsync());
    }

    private sealed class DenyAllAccessPolicy : ISecretAccessPolicy, ISensitiveDataAccessPolicy
    {
        public bool IsAllowed(string accessor, SecretReference reference) => true;
        public bool CanRead(string accessor, SensitiveEnvelope envelope) => false;
    }
}
