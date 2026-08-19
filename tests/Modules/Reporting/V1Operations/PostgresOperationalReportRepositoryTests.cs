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
        _service = new OperationalReportService(_repository);
    }

    [Fact]
    public async Task OpenAndCloseBusinessDayWithReconciledTotalsSuccessfully()
    {
        var date = new DateOnly(2026, 8, 18);
        var openedAt = DateTimeOffset.UtcNow.AddHours(-10);

        // 1. Open Business Day
        var opened = await _service.OpenBusinessDayAsync(date, openedAt);
        opened.Should().NotBeNull();
        opened.Status.Should().Be(BusinessDayStatus.Open);
        opened.BusinessDate.Should().Be(date);

        // Waiter Summaries
        var waiter1 = new WaiterPerformanceRecord(
            SummaryId: Guid.NewGuid(),
            BusinessDate: date,
            WaiterUserId: Guid.NewGuid(),
            OrdersServedCount: 15,
            TotalSalesAmount: 4250.00m,
            CancellationsCount: 1,
            DiscountsAppliedAmount: 150.00m,
            CapturedAt: DateTimeOffset.UtcNow);

        // Print Summaries
        var print1 = new PrintErrorSummaryRecord(
            ErrorSummaryId: Guid.NewGuid(),
            BusinessDate: date,
            StationName: "KitchenStation01",
            TotalPrintJobs: 45,
            FailedPrintJobs: 2,
            RecoveredPrintJobs: 2,
            CapturedAt: DateTimeOffset.UtcNow);

        var closedAt = DateTimeOffset.UtcNow;

        // 2. Close Business Day (Acceptance Evidence #1: Report totals reconcile with source metrics)
        var result = await _service.CloseBusinessDayAsync(
            businessDate: date,
            closedAt: closedAt,
            totalRevenue: 4250.00m,
            totalOrders: 15,
            cancelledItems: 1,
            printFailures: 2,
            waiterSummaries: new[] { waiter1 },
            printSummaries: new[] { print1 });

        result.Should().NotBeNull();
        result.BusinessDay.Status.Should().Be(BusinessDayStatus.Closed);
        result.BusinessDay.TotalRevenue.Should().Be(4250.00m);
        result.BusinessDay.TotalOrdersCount.Should().Be(15);
        result.BusinessDay.TotalCancelledItemsCount.Should().Be(1);
        result.BusinessDay.TotalPrintFailuresCount.Should().Be(2);

        result.WaiterSummaries.Should().HaveCount(1);
        result.WaiterSummaries[0].TotalSalesAmount.Should().Be(4250.00m);

        result.PrintSummaries.Should().HaveCount(1);
        result.PrintSummaries[0].StationName.Should().Be("KitchenStation01");

        // 3. Verify GetFullDailyReportAsync
        var fullReport = await _service.GetFullDailyReportAsync(date);
        fullReport.Should().NotBeNull();
        fullReport!.BusinessDay.TotalRevenue.Should().Be(4250.00m);
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
}

[CollectionDefinition(nameof(ReportingTestFixtureDefinition), DisableParallelization = true)]
public sealed class ReportingTestFixtureDefinition : ICollectionFixture<ReportingTestDatabase>
{
}
