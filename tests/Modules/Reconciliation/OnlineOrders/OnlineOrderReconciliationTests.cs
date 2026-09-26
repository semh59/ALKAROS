using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.OnlineOrders.Tests.Fixtures;
using ALKAROS.Reconciliation.Payments;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Reconciliation.OnlineOrders.Tests;

/// <summary>
/// V12-REC-001 against real Postgres: every local/provider divergence becomes exactly one case with a safe
/// next action; a retry has one effect however many managers press it; a case is resolved only when its
/// source no longer shows the divergence (or, for a divergence that can never end in the source, on a
/// recorded decision). The class fixture's database is shared, so every assertion is scoped to the ids
/// the test itself seeded.
/// </summary>
public sealed class OnlineOrderReconciliationTests : IClassFixture<OnlineOrderReconciliationTestDatabase>
{
    private static readonly Guid Manager = Guid.NewGuid();
    private static readonly Guid SecondManager = Guid.NewGuid();

    private readonly OnlineOrderReconciliationTestDatabase _database;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ReconciliationService _cases;
    private readonly IReadOnlyList<IOnlineOrderSourcePair> _pairs;

    public OnlineOrderReconciliationTests(OnlineOrderReconciliationTestDatabase database)
    {
        _database = database;
        _dataSource = database.DataSource;
        _cases = new ReconciliationService(new PostgresReconciliationRepository(_dataSource));
        _pairs =
        [
            new ProviderAcceptedLocallyRefusedSourcePair(_dataSource),
            new LocallyAcceptedProviderUnknownSourcePair(_dataSource),
            new ProviderEventFailedSourcePair(_dataSource),
            new CancelledAfterHandoverSourcePair(_dataSource),
            new AvailabilityNotDeliveredSourcePair(_dataSource),
            new ProviderTotalMismatchSourcePair(_dataSource),
            new ProviderStatusUnknownSourcePair(_dataSource),
        ];
    }

    private OnlineOrderReconciliationScanner Scanner(params IOnlineOrderSourcePair[] pairs) => new(pairs.Length == 0 ? _pairs : pairs, _cases);

    private OnlineOrderReconciliationActions Actions() => new(_dataSource, _pairs, _cases, new InboxReprocessing());

    private static string NewExternalId() => "ys-" + Guid.NewGuid().ToString("N")[..16];

    private async Task<ReconciliationCaseRecord> ActiveCaseAsync(string key) =>
        await _cases.GetActiveCaseByDedupKeyAsync(key) ?? throw new InvalidOperationException($"No active case for '{key}'.");

    private Task<long> CasesForKeyAsync(string key) =>
        _database.CountAsync("SELECT count(*) FROM reconciliation.cases WHERE deduplication_key = $1;", key);

    [Fact]
    public async Task ProviderAcceptedButLocallyRefusedOpensOneCaseWithASafeNextAction()
    {
        var unmapped = NewExternalId();
        var outOfStock = NewExternalId();
        await _database.SeedInboxAsync(unmapped, "Rejected", detail: new { rejection = "UnmappedSku", providerCancellationRequested = false });
        await _database.SeedInboxAsync(outOfStock, "Diverged", detail: new { reason = "OutOfStock", providerCancellationRequested = true });

        await Scanner().ScanAllAsync();
        await Scanner().ScanAllAsync();

        var unmappedKey = ProviderAcceptedLocallyRefusedSourcePair.DeduplicationPrefix + unmapped;
        var outOfStockKey = ProviderAcceptedLocallyRefusedSourcePair.DeduplicationPrefix + outOfStock;
        (await CasesForKeyAsync(unmappedKey)).Should().Be(1);
        (await CasesForKeyAsync(outOfStockKey)).Should().Be(1);

        var unmappedCase = await ActiveCaseAsync(unmappedKey);
        unmappedCase.CaseType.Should().Be(CaseType.OnlineOrderMismatch);
        unmappedCase.Severity.Should().Be(CaseSeverity.High);
        unmappedCase.SourceBRef.Should().Be($"yemeksepeti:order:{unmapped}");
        var unmappedDetails = OnlineOrderCaseDetails.TryParse(unmappedCase.DetailsJson)!;
        unmappedDetails.Kind.Should().Be(OnlineOrderDivergenceKind.ProviderAcceptedLocallyRefused);
        unmappedDetails.NextAction.Should().Be(OnlineOrderNextAction.ReprocessProviderEvent);
        unmappedDetails.Reason.Should().Be("UnmappedSku");

        OnlineOrderCaseDetails.TryParse((await ActiveCaseAsync(outOfStockKey)).DetailsJson)!.NextAction
            .Should().Be(OnlineOrderNextAction.ResendProviderCancellation);
    }

    [Fact]
    public async Task ARefusalTheProviderAlreadyEndedOpensNoCase()
    {
        var reprocessed = NewExternalId();
        var providerCancelled = NewExternalId();
        var cancellationDelivered = NewExternalId();
        foreach (var id in new[] { reprocessed, providerCancelled, cancellationDelivered })
            await _database.SeedInboxAsync(id, "Diverged", detail: new { reason = "OutOfStock", providerCancellationRequested = true });
        await _database.SeedOnlineOrderAsync(reprocessed, "Accepted", 10m);
        await _database.SeedInboxAsync(providerCancelled, "CancelledBeforeOrder");
        await _database.SeedStatusUpdateAsync(cancellationDelivered, "dispatched");

        await Scanner(new ProviderAcceptedLocallyRefusedSourcePair(_dataSource)).ScanAllAsync();

        foreach (var id in new[] { reprocessed, providerCancelled, cancellationDelivered })
            (await CasesForKeyAsync(ProviderAcceptedLocallyRefusedSourcePair.DeduplicationPrefix + id)).Should().Be(0);
    }

    [Fact]
    public async Task LocallyAcceptedWithTheProviderNeverToldOpensACaseForEachDeadUpdate()
    {
        var externalId = NewExternalId();
        var orderId = await _database.SeedOnlineOrderAsync(externalId, "Served", 245.50m);
        var dead = await _database.SeedStatusUpdateAsync(externalId, "dead");
        var stillTrying = await _database.SeedStatusUpdateAsync(externalId, "pending", attempts: 2);
        var orphan = await _database.SeedStatusUpdateAsync(NewExternalId(), "dead");

        await Scanner().ScanAllAsync();

        var record = await ActiveCaseAsync(LocallyAcceptedProviderUnknownSourcePair.DeduplicationPrefix + dead);
        record.DiscrepancyAmount.Should().Be(245.50m);
        record.SourceARef.Should().Be($"orders.orders:{orderId}");
        OnlineOrderCaseDetails.TryParse(record.DetailsJson)!.NextAction.Should().Be(OnlineOrderNextAction.ResendProviderUpdate);
        (await CasesForKeyAsync(LocallyAcceptedProviderUnknownSourcePair.DeduplicationPrefix + stillTrying)).Should().Be(0);
        // A dead update without a local order is the refused-locally side, not this one.
        (await CasesForKeyAsync(LocallyAcceptedProviderUnknownSourcePair.DeduplicationPrefix + orphan)).Should().Be(0);
    }

    [Fact]
    public async Task TwoManagersRetryingAtOnceRequeueTheDeadUpdateExactlyOnce()
    {
        var externalId = NewExternalId();
        await _database.SeedOnlineOrderAsync(externalId, "Served", 50m);
        var dead = await _database.SeedStatusUpdateAsync(externalId, "dead");
        await Scanner(new LocallyAcceptedProviderUnknownSourcePair(_dataSource)).ScanAllAsync();
        var record = await ActiveCaseAsync(LocallyAcceptedProviderUnknownSourcePair.DeduplicationPrefix + dead);

        var results = await Task.WhenAll(
            Task.Run(() => Actions().RetryAsync(record.CaseId, Manager)),
            Task.Run(() => Actions().RetryAsync(record.CaseId, SecondManager)));

        results.Select(r => r.Outcome).Should().BeEquivalentTo(
            [OnlineOrderRetryOutcome.Requeued, OnlineOrderRetryOutcome.NothingToRetry]);
        (await _database.OutboxStateAsync(dead)).Should().Be(("pending", 0));
        (await _database.CountAsync(
            "SELECT count(*) FROM reconciliation.online_order_retry_attempts WHERE case_id = $1;", record.CaseId)).Should().Be(2);
        (await _cases.GetCaseActionsAsync(record.CaseId)).Count(a => a.ActionType == ActionType.NoteAdded).Should().Be(2);
    }

    [Fact]
    public async Task ACaseIsResolvedOnlyOnceItsSourceAgreesAndNeverOnAStaleVersion()
    {
        var externalId = NewExternalId();
        await _database.SeedOnlineOrderAsync(externalId, "Served", 50m);
        var dead = await _database.SeedStatusUpdateAsync(externalId, "dead");
        await Scanner(new LocallyAcceptedProviderUnknownSourcePair(_dataSource)).ScanAllAsync();
        var record = await ActiveCaseAsync(LocallyAcceptedProviderUnknownSourcePair.DeduplicationPrefix + dead);

        var refused = await Actions().ResolveAsync(record.CaseId, record.RowVersion, "Sağlayıcı aradı.", Manager);
        refused.Outcome.Should().Be(OnlineOrderResolveOutcome.StillDiverged);
        (await _cases.GetCaseByIdAsync(record.CaseId))!.Status.Should().Be(CaseStatus.Open);

        await _database.SetOutboxStatusAsync(dead, "dispatched");
        var stale = () => Actions().ResolveAsync(record.CaseId, record.RowVersion + 1, "Bildirim ulaştı.", Manager);
        await stale.Should().ThrowAsync<ReconciliationConcurrencyException>();

        var resolved = await Actions().ResolveAsync(record.CaseId, record.RowVersion, "Bildirim ulaştı.", Manager);
        resolved.Outcome.Should().Be(OnlineOrderResolveOutcome.Resolved);
        resolved.Case!.Status.Should().Be(CaseStatus.Resolved);
        (await _cases.GetCaseActionsAsync(record.CaseId)).Should().Contain(a => a.ActionType == ActionType.Resolved && a.PerformedBy == Manager);
    }

    [Fact]
    public async Task ReprocessingPutsARefusedEventBackButNeverOneWhoseCancellationWasRequested()
    {
        var unmapped = NewExternalId();
        var outOfStock = NewExternalId();
        var unmappedInbox = await _database.SeedInboxAsync(unmapped, "Rejected", detail: new { rejection = "UnmappedSku", providerCancellationRequested = false });
        var outOfStockInbox = await _database.SeedInboxAsync(outOfStock, "Diverged", detail: new { reason = "OutOfStock", providerCancellationRequested = true });
        var cancellation = await _database.SeedStatusUpdateAsync(outOfStock, "pending", attempts: 1);
        await Scanner(new ProviderAcceptedLocallyRefusedSourcePair(_dataSource)).ScanAllAsync();

        var reprocess = await Actions().RetryAsync((await ActiveCaseAsync(ProviderAcceptedLocallyRefusedSourcePair.DeduplicationPrefix + unmapped)).CaseId, Manager);
        reprocess.Outcome.Should().Be(OnlineOrderRetryOutcome.Requeued);
        (await _database.InboxStateAsync(unmappedInbox)).Should().Be(((string?)null, 0));

        var outOfStockCase = await ActiveCaseAsync(ProviderAcceptedLocallyRefusedSourcePair.DeduplicationPrefix + outOfStock);
        (await Actions().RetryAsync(outOfStockCase.CaseId, Manager)).Outcome.Should().Be(OnlineOrderRetryOutcome.NothingToRetry);
        (await _database.InboxStateAsync(outOfStockInbox)).Outcome.Should().Be("Diverged");

        await _database.SetOutboxStatusAsync(cancellation, "dead");
        (await Actions().RetryAsync(outOfStockCase.CaseId, Manager)).Outcome.Should().Be(OnlineOrderRetryOutcome.Requeued);
        (await _database.OutboxStateAsync(cancellation)).Status.Should().Be("pending");
        (await _database.InboxStateAsync(outOfStockInbox)).Outcome.Should().Be("Diverged");
    }

    [Fact]
    public async Task AReprocessedEventThatIntakeRefusedAgainWithACancellationIsNeverReprocessedTwice()
    {
        var externalId = NewExternalId();
        var inbox = await _database.SeedInboxAsync(externalId, "Rejected", detail: new { rejection = "UnmappedSku", providerCancellationRequested = false });
        await Scanner(new ProviderAcceptedLocallyRefusedSourcePair(_dataSource)).ScanAllAsync();
        var record = await ActiveCaseAsync(ProviderAcceptedLocallyRefusedSourcePair.DeduplicationPrefix + externalId);
        (await Actions().RetryAsync(record.CaseId, Manager)).Outcome.Should().Be(OnlineOrderRetryOutcome.Requeued);

        // Intake processes it again and refuses it again, this time asking the provider to cancel. The open
        // case still carries the reprocess action it was opened with.
        await _database.CountAsync(
            """
            UPDATE online_ordering.yemeksepeti_webhook_inbox
            SET processed_at = now(), processing_outcome = 'Rejected',
                outcome_detail = '{"rejection":"ProductInactive","providerCancellationRequested":true}'::jsonb
            WHERE inbox_id = $1 RETURNING 1;
            """, inbox);

        (await Actions().RetryAsync(record.CaseId, Manager)).Outcome.Should().Be(OnlineOrderRetryOutcome.NothingToRetry);
        (await _database.InboxStateAsync(inbox)).Outcome.Should().Be("Rejected");
    }

    [Fact]
    public async Task AFailedProviderEventIsACaseAndItsRetryProcessesItAgain()
    {
        var externalId = NewExternalId();
        var inbox = await _database.SeedInboxAsync(externalId, "Failed", attempts: 5);
        await Scanner(new ProviderEventFailedSourcePair(_dataSource)).ScanAllAsync();
        var record = await ActiveCaseAsync(ProviderEventFailedSourcePair.DeduplicationPrefix + inbox);
        record.Severity.Should().Be(CaseSeverity.Medium);

        (await Actions().ResolveAsync(record.CaseId, record.RowVersion, "Bakıldı.", Manager)).Outcome
            .Should().Be(OnlineOrderResolveOutcome.StillDiverged);
        (await Actions().RetryAsync(record.CaseId, Manager)).Outcome.Should().Be(OnlineOrderRetryOutcome.Requeued);
        (await _database.InboxStateAsync(inbox)).Should().Be(((string?)null, 0));
        (await Actions().ResolveAsync(record.CaseId, record.RowVersion, "Yeniden işlendi.", Manager)).Outcome
            .Should().Be(OnlineOrderResolveOutcome.Resolved);
    }

    [Fact]
    public async Task ACancellationAfterHandoverNeedsARecordedDecisionAndNeverReopens()
    {
        var externalId = NewExternalId();
        var orderId = await _database.SeedOnlineOrderAsync(externalId, "Served", 180m);
        var evidenceId = "evidence-" + externalId;
        await _database.SeedInboxAsync(externalId, "Diverged", orderId, new { reason = "CancelledAfterHandover", evidenceId, localStatus = "Served" });
        await _database.SeedInboxAsync(externalId, "Diverged", orderId, new { reason = "CancelledAfterHandover", evidenceId, localStatus = "Served" });

        await Scanner(new CancelledAfterHandoverSourcePair(_dataSource)).ScanAllAsync();
        var key = CancelledAfterHandoverSourcePair.DeduplicationPrefix + evidenceId;
        (await CasesForKeyAsync(key)).Should().Be(1);
        var record = await ActiveCaseAsync(key);
        record.Severity.Should().Be(CaseSeverity.Critical);
        record.DiscrepancyAmount.Should().Be(180m);

        (await Actions().RetryAsync(record.CaseId, Manager)).Outcome.Should().Be(OnlineOrderRetryOutcome.NotRetryable);
        var withoutNote = () => Actions().ResolveAsync(record.CaseId, record.RowVersion, "  ", Manager);
        await withoutNote.Should().ThrowAsync<ArgumentException>();

        (await Actions().ResolveAsync(record.CaseId, record.RowVersion, "Sağlayıcı ödemeyi yaptı.", Manager)).Outcome
            .Should().Be(OnlineOrderResolveOutcome.Resolved);
        await Scanner(new CancelledAfterHandoverSourcePair(_dataSource)).ScanAllAsync();
        (await CasesForKeyAsync(key)).Should().Be(1);
        (await _cases.GetActiveCaseByDedupKeyAsync(key)).Should().BeNull();
    }

    [Fact]
    public async Task AProviderSubTotalDifferenceIsACaseForAPersonAndNeverReopens()
    {
        var externalId = NewExternalId();
        var orderId = await _database.SeedOnlineOrderAsync(externalId, "Accepted", 150m);
        await _database.SeedInboxAsync(externalId, "OrderCreated", orderId,
            new { totalsMatch = false, providerSubTotal = 140.00m, localSubTotal = 150.00m });
        var matching = NewExternalId();
        var matchingOrder = await _database.SeedOnlineOrderAsync(matching, "Accepted", 150m);
        await _database.SeedInboxAsync(matching, "OrderCreated", matchingOrder,
            new { totalsMatch = true, providerSubTotal = 150.00m, localSubTotal = 150.00m });

        var pair = new ProviderTotalMismatchSourcePair(_dataSource);
        await Scanner(pair).ScanAllAsync();

        var key = ProviderTotalMismatchSourcePair.DeduplicationPrefix + externalId;
        var record = await ActiveCaseAsync(key);
        record.DiscrepancyAmount.Should().Be(10m);
        record.SourceARef.Should().Be($"orders.orders:{orderId}");
        (await CasesForKeyAsync(ProviderTotalMismatchSourcePair.DeduplicationPrefix + matching)).Should().Be(0);
        (await Actions().RetryAsync(record.CaseId, Manager)).Outcome.Should().Be(OnlineOrderRetryOutcome.NotRetryable);

        (await Actions().ResolveAsync(record.CaseId, record.RowVersion, "Sağlayıcı indirimi, fark kabul edildi.", Manager)).Outcome
            .Should().Be(OnlineOrderResolveOutcome.Resolved);
        await Scanner(pair).ScanAllAsync();
        (await CasesForKeyAsync(key)).Should().Be(1);
    }

    [Fact]
    public async Task AnEventWithAnUnknownProviderStatusIsACaseForAPersonAndNeverReopens()
    {
        var externalId = NewExternalId();
        var inbox = await _database.SeedInboxAsync(externalId, "UnknownStatus", detail: new { evidenceId = "u-1" });
        var pair = new ProviderStatusUnknownSourcePair(_dataSource);

        await Scanner(pair).ScanAllAsync();
        await Scanner(pair).ScanAllAsync();

        var key = ProviderStatusUnknownSourcePair.DeduplicationPrefix + inbox;
        (await CasesForKeyAsync(key)).Should().Be(1);
        var record = await ActiveCaseAsync(key);
        record.Severity.Should().Be(CaseSeverity.High);
        (await Actions().ResolveAsync(record.CaseId, record.RowVersion, "Sağlayıcıyla görüşüldü.", Manager)).Outcome
            .Should().Be(OnlineOrderResolveOutcome.Resolved);
        await Scanner(pair).ScanAllAsync();
        (await CasesForKeyAsync(key)).Should().Be(1);
    }

    [Fact]
    public async Task AnAvailabilityDifferenceBecomesACaseOnlyAfterTheToleranceOrRepeatedFailures()
    {
        var fresh = Guid.NewGuid();
        var old = Guid.NewGuid();
        var failing = Guid.NewGuid();
        await _database.SeedAvailabilityStateAsync("yemeksepeti", fresh, desired: 0, delivered: 4, attempts: 1, desiredMinutesAgo: 1);
        await _database.SeedAvailabilityStateAsync("yemeksepeti", old, desired: 0, delivered: 4, attempts: 0, desiredMinutesAgo: 30);
        await _database.SeedAvailabilityStateAsync("yemeksepeti", failing, desired: 2, delivered: null, attempts: 3, desiredMinutesAgo: 1);

        await Scanner(new AvailabilityNotDeliveredSourcePair(_dataSource)).ScanAllAsync();

        (await CasesForKeyAsync($"online-availability:yemeksepeti:{fresh}")).Should().Be(0);
        var oldCase = await ActiveCaseAsync($"online-availability:yemeksepeti:{old}");
        await ActiveCaseAsync($"online-availability:yemeksepeti:{failing}");
        OnlineOrderCaseDetails.TryParse(oldCase.DetailsJson)!.NextAction.Should().Be(OnlineOrderNextAction.CheckChannelConnection);

        (await Actions().RetryAsync(oldCase.CaseId, Manager)).Outcome.Should().Be(OnlineOrderRetryOutcome.NotRetryable);
        (await Actions().ResolveAsync(oldCase.CaseId, oldCase.RowVersion, "Bağlantı düzeldi.", Manager)).Outcome
            .Should().Be(OnlineOrderResolveOutcome.StillDiverged);
        await _database.MarkAvailabilityDeliveredAsync("yemeksepeti", old);
        (await Actions().ResolveAsync(oldCase.CaseId, oldCase.RowVersion, "Bağlantı düzeldi.", Manager)).Outcome
            .Should().Be(OnlineOrderResolveOutcome.Resolved);
    }

    [Fact]
    public async Task ADismissedRefusalIsNotOpenedAgain()
    {
        var externalId = NewExternalId();
        await _database.SeedInboxAsync(externalId, "Rejected", detail: new { rejection = "UnmappedSku", providerCancellationRequested = false });
        var pair = new ProviderAcceptedLocallyRefusedSourcePair(_dataSource);
        await Scanner(pair).ScanAllAsync();
        var key = ProviderAcceptedLocallyRefusedSourcePair.DeduplicationPrefix + externalId;
        var record = await ActiveCaseAsync(key);

        await _cases.TransitionCaseStatusAsync(new TransitionCaseStatusRequest(record.CaseId, CaseStatus.Dismissed, record.RowVersion, Manager, "Sağlayıcı test siparişi."));
        await Scanner(pair).ScanAllAsync();

        (await CasesForKeyAsync(key)).Should().Be(1);
        (await Actions().RetryAsync(record.CaseId, Manager)).Outcome.Should().Be(OnlineOrderRetryOutcome.CaseNotActive);
    }

    [Fact]
    public async Task RetryAndResolveRefuseCasesThatAreNotOnlineOrderCases()
    {
        var payment = await _cases.CreateOrDeduplicateCaseAsync(new CreateCaseRequest(
            "test-payment:" + Guid.NewGuid(), CaseType.PaymentMismatch, "payments.payments:x", "billing.bills:y", 1m, CaseSeverity.Low, Manager));

        (await Actions().RetryAsync(payment.CaseId, Manager)).Outcome.Should().Be(OnlineOrderRetryOutcome.NotAnOnlineOrderCase);
        (await Actions().ResolveAsync(payment.CaseId, payment.RowVersion, "not", Manager)).Outcome.Should().Be(OnlineOrderResolveOutcome.NotAnOnlineOrderCase);
        (await Actions().RetryAsync(Guid.NewGuid(), Manager)).Outcome.Should().Be(OnlineOrderRetryOutcome.CaseNotFound);
        (await _cases.GetCaseByIdAsync(payment.CaseId))!.Status.Should().Be(CaseStatus.Open);
    }

    [Fact]
    public async Task ConcurrentScansNeverOpenTwoCasesForOneDivergence()
    {
        var externalIds = Enumerable.Range(0, 5).Select(_ => NewExternalId()).ToList();
        foreach (var id in externalIds)
            await _database.SeedInboxAsync(id, "Rejected", detail: new { rejection = "UnmappedSku", providerCancellationRequested = false });

        var scans = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            Task.Run(() => Scanner(new ProviderAcceptedLocallyRefusedSourcePair(_dataSource)).ScanAllAsync())));

        // No scan loses its source to a duplicate-key failure (V1-RMD-312).
        scans.SelectMany(s => s).Should().OnlyContain(r => r.FailureReason == null);
        foreach (var id in externalIds)
            (await CasesForKeyAsync(ProviderAcceptedLocallyRefusedSourcePair.DeduplicationPrefix + id)).Should().Be(1);
    }

    [Fact]
    public async Task AFailingSourceIsReportedAloneAndInTurkish()
    {
        var externalId = NewExternalId();
        await _database.SeedInboxAsync(externalId, "Failed", attempts: 5);

        var results = await Scanner(new ThrowingSourcePair(), new ProviderEventFailedSourcePair(_dataSource)).ScanAllAsync();

        results[0].FailureReason.Should().Be(OnlineOrderReconciliationScanner.SourceUnreadableReason);
        results[1].FailureReason.Should().BeNull();
        results[1].CasesCreatedOrDeduplicated.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task TheRetryTrailMigrationRollsBackAndReapplies()
    {
        await using (var down = _dataSource.CreateCommand(OnlineOrderReconciliationTestDatabase.MigrationScript("down")))
            await down.ExecuteNonQueryAsync();
        (await _database.CountAsync("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'reconciliation' AND table_name = 'online_order_retry_attempts';"))
            .Should().Be(0);
        await using (var up = _dataSource.CreateCommand(OnlineOrderReconciliationTestDatabase.MigrationScript("up")))
            await up.ExecuteNonQueryAsync();
        (await _database.CountAsync("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'reconciliation' AND table_name = 'online_order_retry_attempts';"))
            .Should().Be(1);
    }

    /// <summary>The same binding the Host makes: the OnlineOrdering inbox contract.</summary>
    private sealed class InboxReprocessing : IProviderEventReprocessing
    {
        public Task<int> ReopenForReprocessingAsync(
            Guid inboxId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default) =>
            ALKAROS.OnlineOrdering.Yemeksepeti.OrderNormalization.YemeksepetiInboxProcessingStore.ReopenForReprocessingAsync(
                inboxId, connection, transaction, cancellationToken);
    }

    private sealed class ThrowingSourcePair : IOnlineOrderSourcePair
    {
        public string Kind => "Broken";
        public bool RequiresManualResolution => false;
        public string Name => "Broken";
        public bool IsEnabled => true;
        public string? DisabledReason => null;

        public Task<IReadOnlyList<DetectedDiscrepancy>> ScanAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("relation does not exist");
    }
}
