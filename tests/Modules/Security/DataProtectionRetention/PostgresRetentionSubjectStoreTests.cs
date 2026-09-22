using ALKAROS.Security.DataProtectionRetention.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Security.DataProtectionRetention.Tests;

public sealed class PostgresRetentionSubjectStoreTests : IAsyncLifetime
{
    private readonly DataProtectionRetentionTestDatabase _database = new();
    private readonly Secrets.SecretReference _key = RetentionCryptoFixtures.OldKey;
    private SensitiveData.SensitivePayloadProtector _protector = null!;
    private PostgresRetentionSubjectStore _store = null!;

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    private PostgresRetentionSubjectStore Store()
    {
        _protector = RetentionCryptoFixtures.CreateProtector();
        _store = new PostgresRetentionSubjectStore(_database.DataSource);
        return _store;
    }

    [Fact]
    public async Task InsertThenGetRoundTripsTheRecord()
    {
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);
        var createdAt = DateTimeOffset.UtcNow.AddDays(-1);

        var id = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, legalHold: false, createdAt, default);
        var record = await store.GetAsync(id, default);

        record.Should().NotBeNull();
        record!.Category.Should().Be(DataCategory.ProviderPayloads);
        record.LegalHold.Should().BeFalse();
        record.IsDisposed.Should().BeFalse();
        record.RowVersion.Should().Be(1);
        record.Envelope.Ciphertext.KeyId.Should().Be(_key.Name);
    }

    [Fact]
    public async Task GetPendingExcludesDisposedSubjects()
    {
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);
        var pendingId = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, DateTimeOffset.UtcNow, default);
        var disposedId = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, DateTimeOffset.UtcNow, default);
        await store.MarkDisposedAsync(disposedId, DisposalAction.Anonymize, 1, default);

        var pending = await store.GetPendingAsync(default);

        pending.Select(r => r.Id).Should().Contain(pendingId);
        pending.Select(r => r.Id).Should().NotContain(disposedId);
    }

    [Fact]
    public async Task MarkDisposedAnonymizeOverwritesEnvelopeButKeepsTheRow()
    {
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);
        var id = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, DateTimeOffset.UtcNow, default);

        await store.MarkDisposedAsync(id, DisposalAction.Anonymize, 1, default);
        var record = await store.GetAsync(id, default);

        record.Should().NotBeNull();
        record!.IsDisposed.Should().BeTrue();
        record.Action.Should().Be(DisposalAction.Anonymize);
        record.Envelope.Ciphertext.KeyId.Should().NotBe(_key.Name);
        Assert.Throws<SensitiveData.SensitiveDataException>(
            () => _protector.Unprotect(record.Envelope, _key, RetentionCryptoFixtures.Accessor));
    }

    [Fact]
    public async Task MarkDisposedDeleteKeepsTheEnvelopeUntilPurge()
    {
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);
        var id = await store.InsertAsync(DataCategory.UserCredentials, envelope, false, DateTimeOffset.UtcNow, default);

        await store.MarkDisposedAsync(id, DisposalAction.Delete, 1, default);
        var record = await store.GetAsync(id, default);
        var queue = await store.GetDeletionQueueAsync(default);

        record.Should().NotBeNull();
        record!.Envelope.Ciphertext.KeyId.Should().Be(_key.Name);
        queue.Should().Contain(id);
    }

    [Fact]
    public async Task PurgeIsIdempotent()
    {
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);
        var id = await store.InsertAsync(DataCategory.UserCredentials, envelope, false, DateTimeOffset.UtcNow, default);
        await store.MarkDisposedAsync(id, DisposalAction.Delete, 1, default);

        await store.PurgeAsync(id, default);
        await store.PurgeAsync(id, default);

        (await store.GetAsync(id, default)).Should().BeNull();
    }

    [Fact]
    public async Task MarkDisposedWithStaleRowVersionThrowsConcurrencyException()
    {
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);
        var id = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, DateTimeOffset.UtcNow, default);

        await Assert.ThrowsAsync<RetentionConcurrencyException>(
            () => store.MarkDisposedAsync(id, DisposalAction.Anonymize, expectedRowVersion: 99, default));
    }

    [Fact]
    public async Task SetLegalHoldRoundTrips()
    {
        var store = Store();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(_protector, _key);
        var id = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, DateTimeOffset.UtcNow, default);

        await store.SetLegalHoldAsync(id, true, 1, default);
        var record = await store.GetAsync(id, default);

        record!.LegalHold.Should().BeTrue();
        record.RowVersion.Should().Be(2);
    }
}
