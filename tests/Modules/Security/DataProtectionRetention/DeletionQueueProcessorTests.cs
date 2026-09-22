using ALKAROS.Security.DataProtectionRetention.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Security.DataProtectionRetention.Tests;

public sealed class DeletionQueueProcessorTests : IAsyncLifetime
{
    private readonly DataProtectionRetentionTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task ProcessPurgesEveryQueuedSubjectAndRecordsAnAuditEvent()
    {
        var protector = RetentionCryptoFixtures.CreateProtector();
        var store = new PostgresRetentionSubjectStore(_database.DataSource);
        var audit = new RecordingAuditEventStore();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(protector, RetentionCryptoFixtures.OldKey);
        var id = await store.InsertAsync(DataCategory.UserCredentials, envelope, false, DateTimeOffset.UtcNow, default);
        await store.MarkDisposedAsync(id, DisposalAction.Delete, 1, default);

        var processed = await new DeletionQueueProcessor(store, audit).ProcessAsync("corr-1", default);

        processed.Should().Contain(id);
        (await store.GetAsync(id, default)).Should().BeNull();
        audit.Events.Should().ContainSingle(e => e.AggregateId == id && e.EventName == "RetentionSubjectPurged");
    }

    [Fact]
    public async Task ProcessOnAnEmptyQueueIsANoOp()
    {
        var store = new PostgresRetentionSubjectStore(_database.DataSource);
        var audit = new RecordingAuditEventStore();

        var processed = await new DeletionQueueProcessor(store, audit).ProcessAsync("corr-2", default);

        processed.Should().BeEmpty();
        audit.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessingTwiceIsIdempotent()
    {
        var protector = RetentionCryptoFixtures.CreateProtector();
        var store = new PostgresRetentionSubjectStore(_database.DataSource);
        var audit = new RecordingAuditEventStore();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(protector, RetentionCryptoFixtures.OldKey);
        var id = await store.InsertAsync(DataCategory.UserCredentials, envelope, false, DateTimeOffset.UtcNow, default);
        await store.MarkDisposedAsync(id, DisposalAction.Delete, 1, default);
        var processor = new DeletionQueueProcessor(store, audit);

        await processor.ProcessAsync("corr-3", default);
        var secondRun = await processor.ProcessAsync("corr-3", default);

        secondRun.Should().BeEmpty();
        audit.Events.Should().ContainSingle();
    }

    [Fact]
    public async Task AnonymizedSubjectsAreNeverQueuedForDeletion()
    {
        var protector = RetentionCryptoFixtures.CreateProtector();
        var store = new PostgresRetentionSubjectStore(_database.DataSource);
        var audit = new RecordingAuditEventStore();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(protector, RetentionCryptoFixtures.OldKey);
        var id = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, DateTimeOffset.UtcNow, default);
        await store.MarkDisposedAsync(id, DisposalAction.Anonymize, 1, default);

        var processed = await new DeletionQueueProcessor(store, audit).ProcessAsync("corr-4", default);

        processed.Should().NotContain(id);
        (await store.GetAsync(id, default)).Should().NotBeNull();
    }
}
