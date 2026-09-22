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

    [Fact]
    public async Task WhenPurgeThrowsNoAuditEventIsWrittenForThatSubject()
    {
        // Regression for the audit finding: the audit event used to be
        // appended BEFORE PurgeAsync. If PurgeAsync then failed, the row
        // stayed queued (correct) but a "RetentionSubjectPurged" audit event
        // had already been recorded for a purge that never happened - and
        // the next run's retry would append a second, duplicate event for
        // the same eventual purge. Confirms the corrected ordering
        // (PurgeAsync succeeds first, audit event written after) does not
        // record an event when the purge itself fails.
        var protector = RetentionCryptoFixtures.CreateProtector();
        var realStore = new PostgresRetentionSubjectStore(_database.DataSource);
        var audit = new RecordingAuditEventStore();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(protector, RetentionCryptoFixtures.OldKey);
        var id = await realStore.InsertAsync(DataCategory.UserCredentials, envelope, false, DateTimeOffset.UtcNow, default);
        await realStore.MarkDisposedAsync(id, DisposalAction.Delete, 1, default);
        var failingStore = new PurgeThrowingRetentionSubjectStore(realStore);

        var act = () => new DeletionQueueProcessor(failingStore, audit).ProcessAsync("corr-5", default);

        await act.Should().ThrowAsync<InvalidOperationException>();
        audit.Events.Should().BeEmpty();
        // The row is untouched - still queued for the next run's retry.
        var queue = await realStore.GetDeletionQueueAsync(default);
        queue.Should().Contain(id);
    }
}
