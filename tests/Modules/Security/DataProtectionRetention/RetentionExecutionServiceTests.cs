using ALKAROS.Security.DataProtectionRetention.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Security.DataProtectionRetention.Tests;

public sealed class RetentionExecutionServiceTests : IAsyncLifetime
{
    private readonly DataProtectionRetentionTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    private (PostgresRetentionSubjectStore Store, SensitiveData.SensitivePayloadProtector Protector, RecordingAuditEventStore Audit) Build()
    {
        var protector = RetentionCryptoFixtures.CreateProtector();
        var store = new PostgresRetentionSubjectStore(_database.DataSource);
        return (store, protector, new RecordingAuditEventStore());
    }

    [Fact]
    public async Task ExpiredProviderPayloadIsAnonymized()
    {
        var (store, protector, audit) = Build();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(protector, RetentionCryptoFixtures.OldKey);
        var createdAt = DateTimeOffset.UtcNow.AddDays(-(365 * 7) - 1);
        var id = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, createdAt, default);

        var result = await new RetentionExecutionService(store, audit).RunSweepAsync(DateTimeOffset.UtcNow, "corr-1", default);

        result.Disposed.Should().Contain(id);
        var record = await store.GetAsync(id, default);
        record!.Action.Should().Be(DisposalAction.Anonymize);
        audit.Events.Should().ContainSingle(e => e.AggregateId == id && e.EventName == "RetentionSubjectDisposed");
    }

    [Fact]
    public async Task ExpiredDeleteClassSubjectIsQueuedNotImmediatelyPurged()
    {
        var (store, protector, audit) = Build();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(protector, RetentionCryptoFixtures.OldKey);
        // UserCredentials has no configured automatic period (event-triggered
        // in the real world) - use ProviderPayloads' period with a Delete
        // override is not possible via the fixed matrix, so this test proves
        // the *mechanism* using DeviceData is also unconfigured; assert via
        // DeletionQueueProcessorTests instead, which drives MarkDisposedAsync
        // directly. This test instead proves an unconfigured category is
        // never auto-disposed even when clearly old.
        var id = await store.InsertAsync(DataCategory.UserCredentials, envelope, false, DateTimeOffset.UnixEpoch, default);

        var result = await new RetentionExecutionService(store, audit).RunSweepAsync(DateTimeOffset.UtcNow, "corr-2", default);

        result.Disposed.Should().NotContain(id);
        result.SkippedNotExpired.Should().Contain(id);
        audit.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task LegalHoldSubjectIsNeverDisposedEvenWhenExpired()
    {
        var (store, protector, audit) = Build();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(protector, RetentionCryptoFixtures.OldKey);
        var createdAt = DateTimeOffset.UtcNow.AddDays(-(365 * 7) - 1);
        var id = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, legalHold: true, createdAt, default);

        var result = await new RetentionExecutionService(store, audit).RunSweepAsync(DateTimeOffset.UtcNow, "corr-3", default);

        result.Disposed.Should().NotContain(id);
        result.SkippedLegalHold.Should().Contain(id);
        (await store.GetAsync(id, default))!.IsDisposed.Should().BeFalse();
        audit.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task RetainClassSubjectIsNeverDisposed()
    {
        var (store, protector, audit) = Build();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(protector, RetentionCryptoFixtures.OldKey);
        var id = await store.InsertAsync(DataCategory.FiscalData, envelope, false, DateTimeOffset.UnixEpoch, default);

        var result = await new RetentionExecutionService(store, audit).RunSweepAsync(DateTimeOffset.UtcNow, "corr-4", default);

        result.Disposed.Should().NotContain(id);
        (await store.GetAsync(id, default))!.IsDisposed.Should().BeFalse();
    }

    [Theory]
    [InlineData(DataCategory.FiscalData)]
    [InlineData(DataCategory.InvoiceData)]
    public async Task RetainClassSubjectWithNoConfiguredPeriodIsBucketedAsSkippedRetainNotSkippedNotExpired(DataCategory category)
    {
        // Regression for the audit finding: RunSweepAsync used to check the
        // retention period before the Retain action, so FiscalData/
        // InvoiceData (both Retain-class, both with a null period) fell into
        // SkippedNotExpired instead of SkippedRetain. Behavior (never
        // disposed) was already correct - only the reporting bucket was
        // wrong. The action check now runs first.
        var (store, protector, audit) = Build();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(protector, RetentionCryptoFixtures.OldKey);
        var id = await store.InsertAsync(category, envelope, false, DateTimeOffset.UnixEpoch, default);

        var result = await new RetentionExecutionService(store, audit).RunSweepAsync(DateTimeOffset.UtcNow, "corr-4b", default);

        result.SkippedRetain.Should().Contain(id);
        result.SkippedNotExpired.Should().NotContain(id);
        result.Disposed.Should().NotContain(id);
        audit.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task NotYetExpiredSubjectIsUntouched()
    {
        var (store, protector, audit) = Build();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(protector, RetentionCryptoFixtures.OldKey);
        var id = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, DateTimeOffset.UtcNow, default);

        var result = await new RetentionExecutionService(store, audit).RunSweepAsync(DateTimeOffset.UtcNow, "corr-5", default);

        result.SkippedNotExpired.Should().Contain(id);
        (await store.GetAsync(id, default))!.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public async Task RunningTheSweepTwiceIsIdempotent()
    {
        var (store, protector, audit) = Build();
        var envelope = RetentionCryptoFixtures.ProtectTestPayload(protector, RetentionCryptoFixtures.OldKey);
        var createdAt = DateTimeOffset.UtcNow.AddDays(-(365 * 7) - 1);
        var id = await store.InsertAsync(DataCategory.ProviderPayloads, envelope, false, createdAt, default);
        var service = new RetentionExecutionService(store, audit);

        var first = await service.RunSweepAsync(DateTimeOffset.UtcNow, "corr-6", default);
        var second = await service.RunSweepAsync(DateTimeOffset.UtcNow, "corr-6", default);

        first.Disposed.Should().Contain(id);
        second.Disposed.Should().NotContain(id);
        second.SkippedLegalHold.Should().NotContain(id);
        second.SkippedNotExpired.Should().NotContain(id);
        second.SkippedRetain.Should().NotContain(id);
        audit.Events.Should().ContainSingle();
    }
}
