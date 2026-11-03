using ALKAROS.Reporting.BusinessDayTotals;
using ALKAROS.Reporting.V1Operations.Tests.Fixtures;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Reporting.V1Operations.Tests;

[Collection(nameof(ReportingTestFixtureDefinition))]
public sealed class PostgresOperationalReportRepositoryTests : IClassFixture<ReportingTestDatabase>
{
    private readonly ReportingTestDatabase _db;
    private readonly PostgresOperationalReportRepository _repository;
    private readonly OperationalReportService _service;

    public PostgresOperationalReportRepositoryTests(ReportingTestDatabase db)
    {
        _db = db;
        _repository = new PostgresOperationalReportRepository(_db.DataSource);
        _service = new OperationalReportService(_repository, new FixedBusinessDayTotalsReader());
    }

    private static BusinessDayFigures Figures(OpenItems? open = null, Guid? waiterId = null, Guid? tableId = null) => new(
        Revenue: 4250.00m, Tips: 120.50m, OrderCount: 15, CancelledItems: 1, PrintFailures: 2,
        OpenItems: open ?? new OpenItems(0, 0, 0),
        Waiters: [new WaiterFigure(waiterId ?? Guid.NewGuid(), 15, 4250.00m, 1, 150.00m)],
        PrintStations: [new PrintStationFigure("KitchenStation01", 45, 2, 2)],
        Tables: [new TableFigure(tableId ?? Guid.NewGuid(), 6, 1800.00m)],
        Cash: new CashReconciliationFigure(2, 900m, 895m, -5m, 1, 0));

    private OperationalReportService ServiceWith(BusinessDayFigures figures)
        => new(_repository, new FixedBusinessDayTotalsReader(figures));

    private async Task OpenFreshDayAsync(DateOnly date)
    {
        var active = await _repository.GetActiveBusinessDayAsync();
        if (active is not null)
            await _repository.CloseBusinessDayAsync(active.BusinessDate, DateTimeOffset.UtcNow, 0m, 0, 0, 0);
        await _service.OpenBusinessDayAsync(date, DateTimeOffset.UtcNow.AddHours(-8));
    }

    [Fact]
    public async Task TheCloseStoresEveryServerCountedFigureTheTipsApartFromRevenueAndTheSummaries()
    {
        var date = new DateOnly(2026, 8, 18);
        var tableId = Guid.NewGuid();
        var waiterId = Guid.NewGuid();
        await OpenFreshDayAsync(date);

        var result = await ServiceWith(Figures(waiterId: waiterId, tableId: tableId))
            .CloseBusinessDayAsync(date, DateTimeOffset.UtcNow, acknowledgeOpenItems: false);

        var day = (await _repository.GetBusinessDayByDateAsync(date))!;
        day.Should().BeEquivalentTo(result.BusinessDay, o => o.Excluding(d => d.ClosedAt));
        day.ClosedAt.Should().BeCloseTo(result.BusinessDay.ClosedAt!.Value, TimeSpan.FromMilliseconds(1));
        day.Status.Should().Be(BusinessDayStatus.Closed);
        (day.TotalRevenue, day.TotalTipsAmount, day.TotalOrdersCount).Should().Be((4250.00m, 120.50m, 15));
        (day.TotalCancelledItemsCount, day.TotalPrintFailuresCount).Should().Be((1, 2));
        day.OpenItemsAcknowledged.Should().BeFalse();
        day.OpenItemsAtClose.Should().Be(new OpenItems(0, 0, 0));
        day.TableBreakdown.Should().ContainSingle().Which.Should().Be(new TableFigure(tableId, 6, 1800.00m));
        day.CashReconciliation.Should().Be(new CashReconciliationFigure(2, 900m, 895m, -5m, 1, 0));

        var report = (await _service.GetFullDailyReportAsync(date))!;
        report.WaiterSummaries.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { WaiterUserId = waiterId, OrdersServedCount = 15, TotalSalesAmount = 4250.00m, CancellationsCount = 1, DiscountsAppliedAmount = 150.00m });
        report.PrintSummaries.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { StationName = "KitchenStation01", TotalPrintJobs = 45, FailedPrintJobs = 2, RecoveredPrintJobs = 2 });
    }

    [Fact]
    public async Task OpenItemsNeedAnAcknowledgementAndTheCloseWithoutOneChangesNothing()
    {
        var date = new DateOnly(2026, 8, 20);
        await OpenFreshDayAsync(date);
        var service = ServiceWith(Figures(open: new OpenItems(1, 2, 3)));

        var refused = () => service.CloseBusinessDayAsync(date, DateTimeOffset.UtcNow, acknowledgeOpenItems: false);

        (await refused.Should().ThrowAsync<BusinessDayOpenItemsNotAcknowledgedException>())
            .Which.Should().Match<BusinessDayOpenItemsNotAcknowledgedException>(e => e.CashSessions == 1 && e.Bills == 2 && e.UnsettledPayments == 3);
        (await _repository.GetBusinessDayByDateAsync(date))!.Status.Should().Be(BusinessDayStatus.Open);
        (await _repository.GetWaiterSummariesByDateAsync(date)).Should().BeEmpty();

        var closed = await service.CloseBusinessDayAsync(date, DateTimeOffset.UtcNow, acknowledgeOpenItems: true);

        closed.BusinessDay.OpenItemsAcknowledged.Should().BeTrue();
        var day = (await _repository.GetBusinessDayByDateAsync(date))!;
        day.OpenItemsAtClose.Should().Be(new OpenItems(1, 2, 3));
        day.OpenItemsAcknowledged.Should().BeTrue();
    }

    [Fact]
    public async Task AFailureInsideTheCloseTransactionRollsBackTheDayAndEverySummary()
    {
        var date = new DateOnly(2037, 3, 14);
        await OpenFreshDayAsync(date);

        var act = () => _repository.CloseBusinessDayWithFiguresAsync(
            date, DateTimeOffset.UtcNow, acknowledgeOpenItems: false,
            (_, _, _) => Task.FromResult(Figures()),
            (_, _, _, _, _) => throw new InvalidOperationException("audit write failed"));

        await act.Should().ThrowAsync<InvalidOperationException>();

        (await _repository.GetBusinessDayByDateAsync(date))!.Status.Should().Be(BusinessDayStatus.Open);
        (await _repository.GetWaiterSummariesByDateAsync(date)).Should().BeEmpty();
        (await _repository.GetPrintErrorSummariesByDateAsync(date)).Should().BeEmpty();
        await _repository.CloseBusinessDayAsync(date, DateTimeOffset.UtcNow, 0m, 0, 0, 0);
    }

    [Fact]
    public async Task TheFiguresAreReadInsideTheCloseTransactionOnAnOpenConnection()
    {
        var date = new DateOnly(2037, 3, 15);
        await OpenFreshDayAsync(date);
        var openInside = false;

        await _repository.CloseBusinessDayWithFiguresAsync(
            date, DateTimeOffset.UtcNow, acknowledgeOpenItems: false,
            (_, transaction, _) =>
            {
                openInside = transaction.Connection?.State == System.Data.ConnectionState.Open;
                return Task.FromResult(Figures());
            });

        openInside.Should().BeTrue();
    }

    [Fact]
    public async Task OpeningSameDateTwiceThrowsException()
    {
        var date = new DateOnly(2026, 8, 19);
        var openedAt = DateTimeOffset.UtcNow;

        await _service.OpenBusinessDayAsync(date, openedAt);

        var act = () => _service.OpenBusinessDayAsync(date, openedAt);

        await act.Should().ThrowAsync<BusinessDayAlreadyOpenException>();
    }

    [Fact]
    public async Task ConcurrentOpenAttemptsAllowOnlyOneActiveBusinessDay()
    {
        var active = await _repository.GetActiveBusinessDayAsync();
        if (active is not null)
            await _repository.CloseBusinessDayAsync(active.BusinessDate, DateTimeOffset.UtcNow, 0m, 0, 0, 0);

        var firstDate = new DateOnly(2036, 1, 1);
        var secondDate = new DateOnly(2036, 1, 2);

        var attempts = new[]
        {
            _service.OpenBusinessDayAsync(firstDate, DateTimeOffset.UtcNow),
            _service.OpenBusinessDayAsync(secondDate, DateTimeOffset.UtcNow)
        };

        var results = await Task.WhenAll(attempts.Select(async attempt =>
        {
            try
            {
                await attempt;
                return true;
            }
            catch (InvalidBusinessDayOperationException)
            {
                return false;
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                return false;
            }
        }));

        results.Count(result => result).Should().Be(1);

        var opened = await _repository.GetActiveBusinessDayAsync();
        if (opened is not null)
            await _repository.CloseBusinessDayAsync(opened.BusinessDate, DateTimeOffset.UtcNow, 0m, 0, 0, 0);
    }
}

public sealed class PostgresReportingMigrationTests : IClassFixture<ReportingTestDatabase>
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresReportingMigrationTests(ReportingTestDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _dataSource = database.DataSource;
    }

    [Fact]
    public async Task MigrationDownAndUpExecutesCleanly()
    {
        var downSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "031-operational-reports.down.sql"));
        var upSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "031-operational-reports.up.sql"));

        // 1. Run down.sql
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand(downSql, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Verify tables dropped
        await using (var connection = await _dataSource.OpenConnectionAsync())
        {
            await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('reporting.daily_business_days')::text;", connection))
            {
                var result = await checkCmd.ExecuteScalarAsync();
                result.Should().Be(DBNull.Value);
            }
        }

        // 2. Run up.sql
        await using (var connection = await _dataSource.OpenConnectionAsync())
        await using (var command = new NpgsqlCommand(upSql, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        // Verify tables recreated
        await using (var connection = await _dataSource.OpenConnectionAsync())
        {
            await using (var checkCmd = new NpgsqlCommand("SELECT to_regclass('reporting.daily_business_days')::text;", connection))
            {
                var result = await checkCmd.ExecuteScalarAsync();
                result.Should().Be("reporting.daily_business_days");
            }
        }
    }

    [Fact]
    public async Task TheCloseFiguresMigrationDownAndUpAddsAndDropsItsColumns()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        var downSql = await File.ReadAllTextAsync(Path.Combine(sqlDirectory, "214-business-day-close-figures.down.sql"));
        var upSql = await File.ReadAllTextAsync(Path.Combine(sqlDirectory, "214-business-day-close-figures.up.sql"));

        await Execute(downSql);
        (await ColumnCount()).Should().Be(0);
        await Execute(upSql);
        (await ColumnCount()).Should().Be(7);
        await Execute(upSql);
        (await ColumnCount()).Should().Be(7);
    }

    private async Task Execute(string sql)
    {
        await using var command = _dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> ColumnCount()
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'reporting' AND table_name = 'daily_business_days' AND column_name IN ('total_tips_amount', 'open_cash_sessions_count', 'open_bills_count', 'unsettled_payments_count', 'open_items_acknowledged', 'table_breakdown', 'cash_reconciliation');");
        return (long)(await command.ExecuteScalarAsync())!;
    }
}

[CollectionDefinition(nameof(ReportingTestFixtureDefinition), DisableParallelization = true)]
public sealed class ReportingTestFixtureDefinition : ICollectionFixture<ReportingTestDatabase>
{
}
