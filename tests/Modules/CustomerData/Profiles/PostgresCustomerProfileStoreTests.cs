namespace ALKAROS.CustomerData.Profiles.Tests;

using System.Security.Cryptography;
using System.Text;
using ALKAROS.CustomerData.Profiles.Tests.Fixtures;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Xunit;

/// <summary>
/// V14-CST-001, against a real Postgres database. Mirrors
/// `ALKAROS.Invoicing.Qnb.CredentialRegistration.Tests.PostgresQnbCredentialStoreTests`'s
/// shape exactly.
/// </summary>
public sealed class PostgresCustomerProfileStoreTests : IAsyncLifetime
{
    private readonly CustomerProfileTestDatabase _database = new();
    private InMemorySecretProvider _secretProvider = null!;
    private PostgresCustomerProfileStore _store = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _secretProvider = new InMemorySecretProvider();
        _secretProvider.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        _store = new PostgresCustomerProfileStore(_database.DataSource, _secretProvider);
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    private static CreateCustomerProfileRequest SampleRequest() =>
        new("Ayşe Yılmaz", "05551234567", "ayse@example.com", "İstanbul");

    [Fact]
    public async Task GetOnAnUnknownCustomerReturnsNull()
    {
        var result = await _store.GetAsync(Guid.NewGuid(), CustomerAccessRole.Manager);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreatingAndReadingBackAsManagerRoundTripsEveryField()
    {
        var id = await _store.CreateAsync(SampleRequest());

        var profile = await _store.GetAsync(id, CustomerAccessRole.Manager);

        Assert.NotNull(profile);
        Assert.Equal("Ayşe Yılmaz", profile!.Name);
        Assert.Equal("05551234567", profile.Phone);
        Assert.Equal("ayse@example.com", profile.Email);
        Assert.Equal("İstanbul", profile.Address);
        Assert.False(profile.Anonymized);
        Assert.Equal(1, profile.RowVersion);
    }

    [Fact]
    public async Task CashierAlsoSeesTheFullProfile()
    {
        var id = await _store.CreateAsync(SampleRequest());

        var profile = await _store.GetAsync(id, CustomerAccessRole.Cashier);

        Assert.Equal("Ayşe Yılmaz", profile!.Name);
    }

    [Fact]
    public async Task AnyOtherRoleGetsAFullyRedactedProfileNotAnError()
    {
        var id = await _store.CreateAsync(SampleRequest());

        var profile = await _store.GetAsync(id, CustomerAccessRole.Other);

        Assert.NotNull(profile);
        Assert.Null(profile!.Name);
        Assert.Null(profile.Phone);
        Assert.Null(profile.Email);
        Assert.Null(profile.Address);
        // The id and lifecycle fields are not PII and still come back -
        // referential integrity for an order/invoice history is unaffected.
        Assert.Equal(id, profile.CustomerId);
    }

    [Fact]
    public async Task TheDatabaseNeverStoresContactFieldsInPlaintext()
    {
        var id = await _store.CreateAsync(SampleRequest());

        var envelopeBytes = await _database.RawEnvelopeBytesAsync(id);
        var asLatin1 = Encoding.Latin1.GetString(envelopeBytes);
        Assert.DoesNotContain("05551234567", asLatin1, StringComparison.Ordinal);
        Assert.DoesNotContain("ayse@example.com", asLatin1, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateContactReplacesEveryFieldAndBumpsRowVersion()
    {
        var id = await _store.CreateAsync(SampleRequest());

        await _store.UpdateContactAsync(id, new UpdateCustomerContactRequest("Yeni İsim", null, "new@example.com", null), expectedRowVersion: 1);

        var profile = await _store.GetAsync(id, CustomerAccessRole.Manager);
        Assert.Equal("Yeni İsim", profile!.Name);
        Assert.Null(profile.Phone);
        Assert.Equal("new@example.com", profile.Email);
        Assert.Null(profile.Address);
        Assert.Equal(2, profile.RowVersion);
    }

    [Fact]
    public async Task UpdateContactWithAStaleRowVersionThrowsConcurrencyException()
    {
        var id = await _store.CreateAsync(SampleRequest());
        await _store.UpdateContactAsync(id, new UpdateCustomerContactRequest("First edit", null, null, null), expectedRowVersion: 1);

        await Assert.ThrowsAsync<CustomerProfileConcurrencyException>(() =>
            _store.UpdateContactAsync(id, new UpdateCustomerContactRequest("Stale edit", null, null, null), expectedRowVersion: 1));
    }

    [Fact]
    public async Task UpdateContactOnAnUnknownCustomerThrowsNotFound()
    {
        await Assert.ThrowsAsync<CustomerProfileNotFoundException>(() =>
            _store.UpdateContactAsync(Guid.NewGuid(), new UpdateCustomerContactRequest("X", null, null, null), expectedRowVersion: 1));
    }

    [Fact]
    public async Task AnonymizeWipesContactFieldsButKeepsTheRecordAndItsId()
    {
        var id = await _store.CreateAsync(SampleRequest());

        await _store.AnonymizeAsync(id, expectedRowVersion: 1);

        var profile = await _store.GetAsync(id, CustomerAccessRole.Manager);
        Assert.NotNull(profile);
        Assert.True(profile!.Anonymized);
        Assert.Null(profile.Name);
        Assert.Null(profile.Phone);
        Assert.Null(profile.Email);
        Assert.Null(profile.Address);
        Assert.Equal(id, profile.CustomerId);
    }

    [Fact]
    public async Task AnonymizeIsIdempotentWhenTheProfileIsAlreadyAnonymized()
    {
        var id = await _store.CreateAsync(SampleRequest());
        await _store.AnonymizeAsync(id, expectedRowVersion: 1);

        // A retention sweep that retries after a timeout must not fail just
        // because the desired end state was already reached - even with the
        // caller's now-stale row version.
        var exception = await Record.ExceptionAsync(() => _store.AnonymizeAsync(id, expectedRowVersion: 1));

        Assert.Null(exception);
    }

    [Fact]
    public async Task AnonymizeOnAnUnknownCustomerThrowsNotFound()
    {
        await Assert.ThrowsAsync<CustomerProfileNotFoundException>(() =>
            _store.AnonymizeAsync(Guid.NewGuid(), expectedRowVersion: 1));
    }

    [Fact]
    public async Task AnonymizeWithAGenuinelyStaleRowVersionThrowsConcurrencyException()
    {
        var id = await _store.CreateAsync(SampleRequest());
        await _store.UpdateContactAsync(id, new UpdateCustomerContactRequest("Edited first", null, null, null), expectedRowVersion: 1);

        await Assert.ThrowsAsync<CustomerProfileConcurrencyException>(() =>
            _store.AnonymizeAsync(id, expectedRowVersion: 1));
    }

    [Fact]
    public async Task UpdateContactOnAnAlreadyAnonymizedProfileThrows()
    {
        var id = await _store.CreateAsync(SampleRequest());
        await _store.AnonymizeAsync(id, expectedRowVersion: 1);

        await Assert.ThrowsAsync<CustomerProfileAnonymizedException>(() =>
            _store.UpdateContactAsync(id, new UpdateCustomerContactRequest("Should not apply", null, null, null), expectedRowVersion: 2));
    }

    [Fact]
    public async Task ReadingFailsClosedRatherThanReturningGarbageWhenTheMasterKeyChanged()
    {
        var id = await _store.CreateAsync(SampleRequest());

        _secretProvider.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        await Assert.ThrowsAsync<SensitiveDataEncryptionException>(() => _store.GetAsync(id, CustomerAccessRole.Manager));
    }

    [Fact]
    public async Task AProfileWithNoContactDetailsAtAllStillRoundTrips()
    {
        var id = await _store.CreateAsync(new CreateCustomerProfileRequest(null, null, null, null));

        var profile = await _store.GetAsync(id, CustomerAccessRole.Manager);

        Assert.NotNull(profile);
        Assert.Null(profile!.Name);
        Assert.Null(profile.Phone);
        Assert.Null(profile.Email);
        Assert.Null(profile.Address);
        Assert.False(profile.Anonymized);
    }
}
