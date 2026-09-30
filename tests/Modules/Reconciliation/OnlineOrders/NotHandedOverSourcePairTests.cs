using ALKAROS.Reconciliation.CaseFoundation;
using ALKAROS.Reconciliation.OnlineOrders.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Reconciliation.OnlineOrders.Tests;

/// <summary>
/// An online order left open for hours is shown as one case per platform order, is never closed by the system,
/// resolves once staff hand it over or cancel it, and stays closed after a person dismissed it.
/// </summary>
public sealed class NotHandedOverSourcePairTests : IClassFixture<OnlineOrderReconciliationTestDatabase>
{
    private static readonly Guid Manager = Guid.NewGuid();

    private readonly OnlineOrderReconciliationTestDatabase _database;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ReconciliationService _cases;
    private readonly NotHandedOverSourcePair _pair;

    public NotHandedOverSourcePairTests(OnlineOrderReconciliationTestDatabase database)
    {
        _database = database;
        _dataSource = database.DataSource;
        _cases = new ReconciliationService(new PostgresReconciliationRepository(_dataSource));
        _pair = new NotHandedOverSourcePair(_dataSource);
    }

    private static string NewExternalId() => "ys-" + Guid.NewGuid().ToString("N")[..16];

    private static string KeyOf(string provider, string externalId) => NotHandedOverSourcePair.DeduplicationPrefix + provider + ":" + externalId;

    private async Task<Guid> SeedAgedOrderAsync(string externalId, string status, TimeSpan age, string provider = "yemeksepeti")
    {
        var orderId = await _database.SeedOnlineOrderAsync(externalId, status, 75m, provider);
        await using var command = _dataSource.CreateCommand("UPDATE orders.orders SET created_at = now() - $1 WHERE order_id = $2;");
        command.Parameters.AddWithValue(age);
        command.Parameters.AddWithValue(orderId);
        await command.ExecuteNonQueryAsync();
        return orderId;
    }

    private async Task ScanAsync() => await new OnlineOrderReconciliationScanner([_pair], _cases).ScanAllAsync();

    private Task<long> CasesAsync(string key) =>
        _database.CountAsync("SELECT count(*) FROM reconciliation.cases WHERE deduplication_key = $1;", key);

    private OnlineOrderReconciliationActions Actions() => new(_dataSource, [_pair], _cases, new NoReprocessing());

    [Theory]
    [InlineData("Accepted")]
    [InlineData("Preparing")]
    [InlineData("Ready")]
    public async Task AnOrderOpenPastTheLimitOpensOneCaseWithNothingToRetry(string status)
    {
        var externalId = NewExternalId();
        var orderId = await SeedAgedOrderAsync(externalId, status, NotHandedOverSourcePair.OpenLimit + TimeSpan.FromMinutes(5));

        await ScanAsync();
        await ScanAsync();

        var key = KeyOf("yemeksepeti", externalId);
        (await CasesAsync(key)).Should().Be(1);
        var record = (await _cases.GetActiveCaseByDedupKeyAsync(key))!;
        record.CaseType.Should().Be(CaseType.OnlineOrderMismatch);
        record.Severity.Should().Be(CaseSeverity.High);
        record.SourceARef.Should().Be($"orders.orders:{orderId}");
        var details = OnlineOrderCaseDetails.TryParse(record.DetailsJson)!;
        (details.Kind, details.NextAction, details.Provider).Should().Be((OnlineOrderDivergenceKind.NotHandedOver, OnlineOrderNextAction.HandOverOrCancelOrder, "yemeksepeti"));
        (await Actions().RetryAsync(record.CaseId, Manager)).Outcome.Should().Be(OnlineOrderRetryOutcome.NotRetryable);
    }

    [Fact]
    public async Task ARecentOrAlreadyFinishedOrderOpensNoCase()
    {
        var recent = NewExternalId();
        var handedOver = NewExternalId();
        await SeedAgedOrderAsync(recent, "Accepted", NotHandedOverSourcePair.OpenLimit - TimeSpan.FromMinutes(5));
        await SeedAgedOrderAsync(handedOver, "Completed", TimeSpan.FromHours(30));

        await ScanAsync();

        (await CasesAsync(KeyOf("yemeksepeti", recent))).Should().Be(0);
        (await CasesAsync(KeyOf("yemeksepeti", handedOver))).Should().Be(0);
    }

    [Fact]
    public async Task TheSameExternalIdOnTwoPlatformsOpensTwoCases()
    {
        var externalId = NewExternalId();
        var age = TimeSpan.FromHours(5);
        await SeedAgedOrderAsync(externalId, "Accepted", age, "yemeksepeti");
        await SeedAgedOrderAsync(externalId, "Accepted", age, "trendyol-go");

        await ScanAsync();

        (await CasesAsync(KeyOf("yemeksepeti", externalId))).Should().Be(1);
        (await CasesAsync(KeyOf("trendyol-go", externalId))).Should().Be(1);
    }

    [Fact]
    public async Task TheCaseResolvesOnlyAfterTheOrderIsHandedOver()
    {
        var externalId = NewExternalId();
        var orderId = await SeedAgedOrderAsync(externalId, "Accepted", TimeSpan.FromHours(4));
        await ScanAsync();
        var record = (await _cases.GetActiveCaseByDedupKeyAsync(KeyOf("yemeksepeti", externalId)))!;

        var refused = await Actions().ResolveAsync(record.CaseId, record.RowVersion, "Kontrol edildi.", Manager);
        refused.Outcome.Should().Be(OnlineOrderResolveOutcome.StillDiverged);

        await using (var command = _dataSource.CreateCommand("UPDATE orders.orders SET status = 'Completed' WHERE order_id = $1;"))
        {
            command.Parameters.AddWithValue(orderId);
            await command.ExecuteNonQueryAsync();
        }

        var resolved = await Actions().ResolveAsync(record.CaseId, record.RowVersion, "Sipariş teslim edildi.", Manager);
        resolved.Outcome.Should().Be(OnlineOrderResolveOutcome.Resolved);
    }

    [Fact]
    public async Task ADismissedCaseIsNotOpenedAgain()
    {
        var externalId = NewExternalId();
        await SeedAgedOrderAsync(externalId, "Accepted", TimeSpan.FromHours(6));
        await ScanAsync();
        var key = KeyOf("yemeksepeti", externalId);
        var record = (await _cases.GetActiveCaseByDedupKeyAsync(key))!;

        await _cases.TransitionCaseStatusAsync(new TransitionCaseStatusRequest(record.CaseId, CaseStatus.Dismissed, record.RowVersion, Manager, "Bilinçli olarak izlenmiyor."));
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
