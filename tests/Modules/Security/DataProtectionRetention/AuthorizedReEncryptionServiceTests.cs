using ALKAROS.SensitiveData;
using ALKAROS.Security.DataProtectionRetention.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Security.DataProtectionRetention.Tests;

public sealed class AuthorizedReEncryptionServiceTests : IAsyncLifetime
{
    private readonly DataProtectionRetentionTestDatabase _database = new();
    private static readonly Guid Actor = Guid.NewGuid();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task ReEncryptsUnderTheNewKeyAndPreservesThePlaintextAndCreatedAt()
    {
        var protector = RetentionCryptoFixtures.CreateProtector();
        var store = new PostgresRetentionSubjectStore(_database.DataSource);
        var audit = new RecordingAuditEventStore();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(protector, RetentionCryptoFixtures.OldKey);
        var createdAt = DateTimeOffset.UtcNow.AddDays(-30);
        var id = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, createdAt, default);
        var service = new AuthorizedReEncryptionService(store, RetentionCryptoFixtures.CreateSecretProvider(), audit);

        var changed = await service.ReEncryptAsync(id, RetentionCryptoFixtures.OldKey, RetentionCryptoFixtures.NewKey, Actor, "corr-1", default);

        changed.Should().BeTrue();
        var record = await store.GetAsync(id, default);
        record!.Envelope.Ciphertext.KeyId.Should().Be(RetentionCryptoFixtures.NewKey.Name);
        // Postgres timestamptz has microsecond precision; DateTimeOffset has
        // 100ns ticks, so an exact round-trip comparison is not meaningful.
        record.CreatedAt.Should().BeCloseTo(createdAt, TimeSpan.FromMilliseconds(1));
        var payload = protector.Unprotect(record.Envelope, RetentionCryptoFixtures.NewKey, RetentionCryptoFixtures.Accessor);
        payload.Fields["value"].Should().Be("provider-response-body");
        audit.Events.Should().ContainSingle(e => e.AggregateId == id && e.EventName == "RetentionSubjectReEncrypted" && e.ActorId == Actor);
    }

    [Fact]
    public async Task ReEncryptingASubjectAlreadyOnTheTargetKeyIsANoOp()
    {
        var protector = RetentionCryptoFixtures.CreateProtector();
        var store = new PostgresRetentionSubjectStore(_database.DataSource);
        var audit = new RecordingAuditEventStore();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(protector, RetentionCryptoFixtures.NewKey);
        var id = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, DateTimeOffset.UtcNow, default);
        var service = new AuthorizedReEncryptionService(store, RetentionCryptoFixtures.CreateSecretProvider(), audit);

        var changed = await service.ReEncryptAsync(id, RetentionCryptoFixtures.OldKey, RetentionCryptoFixtures.NewKey, Actor, "corr-2", default);

        changed.Should().BeFalse();
        audit.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task ReEncryptingADisposedSubjectThrows()
    {
        var protector = RetentionCryptoFixtures.CreateProtector();
        var store = new PostgresRetentionSubjectStore(_database.DataSource);
        var audit = new RecordingAuditEventStore();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(protector, RetentionCryptoFixtures.OldKey);
        var id = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, DateTimeOffset.UtcNow, default);
        await store.MarkDisposedAsync(id, DisposalAction.Anonymize, 1, default);
        var service = new AuthorizedReEncryptionService(store, RetentionCryptoFixtures.CreateSecretProvider(), audit);

        await Assert.ThrowsAsync<RetentionSubjectDisposedException>(
            () => service.ReEncryptAsync(id, RetentionCryptoFixtures.OldKey, RetentionCryptoFixtures.NewKey, Actor, "corr-3", default));
    }

    [Fact]
    public async Task ReEncryptingAMissingSubjectThrows()
    {
        var protector = RetentionCryptoFixtures.CreateProtector();
        var store = new PostgresRetentionSubjectStore(_database.DataSource);
        var audit = new RecordingAuditEventStore();
        var service = new AuthorizedReEncryptionService(store, RetentionCryptoFixtures.CreateSecretProvider(), audit);

        await Assert.ThrowsAsync<RetentionSubjectNotFoundException>(
            () => service.ReEncryptAsync(Guid.NewGuid(), RetentionCryptoFixtures.OldKey, RetentionCryptoFixtures.NewKey, Actor, "corr-4", default));
    }

    [Fact]
    public async Task ReEncryptingWithTheWrongOldKeyFailsClosed()
    {
        var protector = RetentionCryptoFixtures.CreateProtector();
        var store = new PostgresRetentionSubjectStore(_database.DataSource);
        var audit = new RecordingAuditEventStore();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(protector, RetentionCryptoFixtures.OldKey);
        var id = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, DateTimeOffset.UtcNow, default);
        var service = new AuthorizedReEncryptionService(store, RetentionCryptoFixtures.CreateSecretProvider(), audit);

        // Envelope was encrypted with OldKey; claiming NewKey as the old key
        // fails Unprotect's key-identity check (not the "already on the
        // target key" idempotency shortcut, since the target here is also
        // NewKey, which differs from the envelope's real OldKey id).
        await Assert.ThrowsAsync<SensitiveDataException>(
            () => service.ReEncryptAsync(id, RetentionCryptoFixtures.NewKey, RetentionCryptoFixtures.NewKey, Actor, "corr-5", default));
    }
}
