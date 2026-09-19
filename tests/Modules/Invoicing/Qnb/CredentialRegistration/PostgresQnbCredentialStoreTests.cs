namespace ALKAROS.Invoicing.Qnb.CredentialRegistration.Tests;

using System.Security.Cryptography;
using System.Text;
using ALKAROS.Invoicing.Qnb.CredentialRegistration.Tests.Fixtures;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Xunit;

/// <summary>
/// V14-QNB-006, against a real Postgres database. Mirrors
/// `ALKAROS.Payments.Token.TerminalCredential.Tests.PostgresTokenTerminalCredentialStoreTests`'s
/// shape exactly.
/// </summary>
public sealed class PostgresQnbCredentialStoreTests : IAsyncLifetime
{
    private readonly QnbCredentialTestDatabase _database = new();
    private InMemorySecretProvider _secretProvider = null!;
    private PostgresQnbCredentialStore _store = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _secretProvider = new InMemorySecretProvider();
        _secretProvider.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        _store = new PostgresQnbCredentialStore(_database.DataSource, _secretProvider);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    private static SaveQnbCredentialRequest SampleRequest(string password = "cs-super-secret-abc123") =>
        new("UserID", password, "3250566851");

    [Fact]
    public async Task WithNothingSavedYetStatusIsNotConfigured()
    {
        var status = await _store.GetStatusAsync();
        Assert.False(status.Configured);
        Assert.Null(status.UpdatedAt);

        var resolved = await _store.ResolvePasswordAsync();
        Assert.Null(resolved);
    }

    [Fact]
    public async Task SavingAndResolvingRoundTripsEveryField()
    {
        await _store.SaveAsync(SampleRequest(), Guid.NewGuid());

        var resolvedPassword = await _store.ResolvePasswordAsync();
        Assert.Equal("cs-super-secret-abc123", resolvedPassword);

        var status = await _store.GetStatusAsync();
        Assert.True(status.Configured);
        Assert.NotNull(status.UpdatedAt);
        Assert.Equal("UserID", status.UserId);
        Assert.Equal("3250566851", status.VergiTcKimlikNo);
    }

    [Fact]
    public async Task TheDatabaseNeverStoresThePasswordInPlaintext()
    {
        const string password = "cs-super-secret-xyz";
        await _store.SaveAsync(SampleRequest(password), Guid.NewGuid());

        var envelopeBytes = await _database.RawEnvelopeBytesAsync();
        var asLatin1 = Encoding.Latin1.GetString(envelopeBytes);
        Assert.DoesNotContain(password, asLatin1, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SavingASecondCredentialReplacesTheFirstOneEntirely()
    {
        await _store.SaveAsync(SampleRequest("first-secret"), Guid.NewGuid());
        await _store.SaveAsync(new SaveQnbCredentialRequest("UserID2", "second-secret", "9999999999"), Guid.NewGuid());

        var resolved = await _store.ResolvePasswordAsync();
        Assert.Equal("second-secret", resolved);

        var status = await _store.GetStatusAsync();
        Assert.Equal("UserID2", status.UserId);
        Assert.Equal("9999999999", status.VergiTcKimlikNo);
    }

    [Fact]
    public async Task ResolvingFailsClosedRatherThanReturningGarbageWhenTheMasterKeyChanged()
    {
        await _store.SaveAsync(SampleRequest(), Guid.NewGuid());

        _secretProvider.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        await Assert.ThrowsAsync<SensitiveDataEncryptionException>(() => _store.ResolvePasswordAsync());
    }
}
