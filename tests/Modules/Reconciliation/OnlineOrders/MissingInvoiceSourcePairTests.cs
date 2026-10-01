using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.OnlineOrders.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Reconciliation.OnlineOrders.Tests;

/// <summary>
/// A delivered online order without an invoice draft is shown as one case while the seven-day invoicing time is
/// still running, names the next step, resolves once a draft exists, and stays closed after a person dismissed it.
/// </summary>
public sealed class MissingInvoiceSourcePairTests : IClassFixture<OnlineOrderReconciliationTestDatabase>
{
    private static readonly Guid Manager = Guid.NewGuid();

    private readonly OnlineOrderReconciliationTestDatabase _database;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ReconciliationService _cases;
    private readonly MissingInvoiceSourcePair _pair;

    public MissingInvoiceSourcePairTests(OnlineOrderReconciliationTestDatabase database)
    {
        _database = database;
        _dataSource = database.DataSource;
        _cases = new ReconciliationService(new PostgresReconciliationRepository(_dataSource));
        _pair = new MissingInvoiceSourcePair(_dataSource);
    }

    private static string NewExternalId() => "ys-" + Guid.NewGuid().ToString("N")[..16];

    private static string KeyOf(string externalId) => MissingInvoiceSourcePair.DeduplicationPrefix + "yemeksepeti:" + externalId;

    private async Task<Guid> SeedDeliveredOrderAsync(string externalId, TimeSpan closedAgo, string status = "Completed")
    {
        var orderId = await _database.SeedOnlineOrderAsync(externalId, status, 75m);
        await ExecuteAsync("UPDATE orders.orders SET closed_at = now() - $1, updated_at = now() - $1 WHERE order_id = $2;", closedAgo, orderId);
        return orderId;
    }

    private async Task ExecuteAsync(string sql, params object[] parameters)
    {
        await using var command = _dataSource.CreateCommand(sql);
        foreach (var value in parameters)
            command.Parameters.AddWithValue(value);
        await command.ExecuteNonQueryAsync();
    }

    private Task AddDraftAsync(Guid orderId, string externalId) => ExecuteAsync(
        """
        INSERT INTO invoicing.order_invoices
            (invoice_id, order_id, provider, external_order_id, order_number, profile, ubl_profile_id, invoice_type_code, currency_code,
             issue_date, service_date, buyer_kind, seller_legal_name, seller_tax_id_kind, seller_tax_id_number, seller_tax_office,
             seller_address, web_address, line_extension_amount, tax_total, payable_amount)
        VALUES ($1, $2, 'yemeksepeti', $3, 'N-1', 'EArsiv', 'EARSIVFATURA', 'SATIS', 'TRY',
                current_date, current_date, 'FinalConsumer', 'Test', 'Vkn', '1234567890', 'Test', 'Test', 'https://www.yemeksepeti.com',
                100, 0, 100);
        """, Guid.NewGuid(), orderId, externalId);

    private async Task ScanAsync() => await new OnlineOrderReconciliationScanner([_pair], _cases).ScanAllAsync();

    private Task<long> CasesAsync(string key) =>
        _database.CountAsync("SELECT count(*) FROM reconciliation.cases WHERE deduplication_key = $1;", key);

    private OnlineOrderReconciliationActions Actions() => new(_dataSource, [_pair], _cases, new NoReprocessing());

    private async Task<OnlineOrderCaseDetails> DetailsOfAsync(string externalId) =>
        OnlineOrderCaseDetails.TryParse((await _cases.GetActiveCaseByDedupKeyAsync(KeyOf(externalId)))!.DetailsJson)!;

    [Theory]
    [InlineData("Served")]
    [InlineData("Completed")]
    public async Task ADeliveredOrderWithoutADraftOpensOneCaseWithNothingToRetry(string status)
    {
        var externalId = NewExternalId();
        var orderId = await SeedDeliveredOrderAsync(externalId, TimeSpan.FromHours(2), status);

        await ScanAsync();
        await ScanAsync();

        var key = KeyOf(externalId);
        (await CasesAsync(key)).Should().Be(1);
        var record = (await _cases.GetActiveCaseByDedupKeyAsync(key))!;
        record.CaseType.Should().Be(CaseType.OnlineOrderMismatch);
        record.SourceARef.Should().Be($"orders.orders:{orderId}");
        var details = OnlineOrderCaseDetails.TryParse(record.DetailsJson)!;
        (details.Kind, details.Provider).Should().Be((OnlineOrderDivergenceKind.MissingInvoice, "yemeksepeti"));
        (await Actions().RetryAsync(record.CaseId, Manager)).Outcome.Should().Be(OnlineOrderRetryOutcome.NotRetryable);
    }

    [Fact]
    public async Task OnlyAnOrderPastTheGraceTimeAndInsideTheInvoicingWindowOpensACase()
    {
        var justClosed = NewExternalId();
        var tooOld = NewExternalId();
        var stillOpen = NewExternalId();
        await SeedDeliveredOrderAsync(justClosed, MissingInvoiceSourcePair.GracePeriod - TimeSpan.FromMinutes(10));
        await SeedDeliveredOrderAsync(tooOld, MissingInvoiceSourcePair.InvoicingWindow + TimeSpan.FromHours(1));
        await SeedDeliveredOrderAsync(stillOpen, TimeSpan.FromHours(3), "Accepted");

        await ScanAsync();

        foreach (var id in new[] { justClosed, tooOld, stillOpen })
            (await CasesAsync(KeyOf(id))).Should().Be(0);
    }

    [Fact]
    public async Task TheNextStepIsToEnterTheSellerDetailsUntilAProfileExistsThenToIssueByHand()
    {
        var before = NewExternalId();
        await SeedDeliveredOrderAsync(before, TimeSpan.FromHours(2));
        await ScanAsync();
        (await DetailsOfAsync(before)).NextAction.Should().Be(OnlineOrderNextAction.EnterSellerProfile);

        await ExecuteAsync(
            """
            INSERT INTO invoicing.seller_profile (legal_name, tax_id_kind, tax_id_number, tax_office, address, district, city)
            VALUES ('Test Lokanta', 'Vkn', '1234567890', 'Kadıköy', 'Test Sk. 1', 'Kadıköy', 'İstanbul');
            """);
        var after = NewExternalId();
        await SeedDeliveredOrderAsync(after, TimeSpan.FromHours(2));
        await ScanAsync();
        (await DetailsOfAsync(after)).NextAction.Should().Be(OnlineOrderNextAction.IssueInvoiceManually);
    }

    [Fact]
    public async Task TheCaseResolvesOnlyAfterTheDraftExists()
    {
        var externalId = NewExternalId();
        var orderId = await SeedDeliveredOrderAsync(externalId, TimeSpan.FromHours(4));
        await ScanAsync();
        var record = (await _cases.GetActiveCaseByDedupKeyAsync(KeyOf(externalId)))!;

        var refused = await Actions().ResolveAsync(record.CaseId, record.RowVersion, "Kontrol edildi.", Manager);
        refused.Outcome.Should().Be(OnlineOrderResolveOutcome.StillDiverged);

        await AddDraftAsync(orderId, externalId);

        var resolved = await Actions().ResolveAsync(record.CaseId, record.RowVersion, "Taslak açıldı.", Manager);
        resolved.Outcome.Should().Be(OnlineOrderResolveOutcome.Resolved);
    }

    [Fact]
    public async Task ADismissedCaseIsNotOpenedAgain()
    {
        var externalId = NewExternalId();
        await SeedDeliveredOrderAsync(externalId, TimeSpan.FromHours(6));
        await ScanAsync();
        var key = KeyOf(externalId);
        var record = (await _cases.GetActiveCaseByDedupKeyAsync(key))!;

        await _cases.TransitionCaseStatusAsync(new TransitionCaseStatusRequest(record.CaseId, CaseStatus.Dismissed, record.RowVersion, Manager, "Fatura elle kesildi."));
        await ScanAsync();

        (await CasesAsync(key)).Should().Be(1);
        (await _cases.GetActiveCaseByDedupKeyAsync(key)).Should().BeNull();
    }

    private sealed class NoReprocessing : IProviderEventReprocessing
    {
        public Task<int> ReopenForReprocessingAsync(
            Guid inboxId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}
