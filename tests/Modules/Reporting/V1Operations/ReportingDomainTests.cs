using FluentAssertions;
using Xunit;

namespace ALKAROS.Reporting.V1Operations.Tests;

public sealed class ReportingDomainTests
{
    private readonly OperationalReportService _service;

    public ReportingDomainTests()
    {
        _service = new OperationalReportService(new FakeReportingRepository());
    }

    [Fact]
    public void CalculateServiceWindowComputesCorrectBoundsAcrossMidnight()
    {
        var businessDate = new DateOnly(2026, 8, 18);
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");

        var filter = _service.CalculateServiceWindow(businessDate, tz);

        filter.BusinessDate.Should().Be(businessDate);
        filter.WindowStart.Hour.Should().Be(6);
        filter.WindowStart.Day.Should().Be(18);
        filter.WindowEnd.Hour.Should().Be(5);
        filter.WindowEnd.Day.Should().Be(19);
    }

    private sealed class FakeReportingRepository : IOperationalReportRepository
    {
        public Task<BusinessDayRecord> OpenBusinessDayAsync(DateOnly businessDate, DateTimeOffset openedAt, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<BusinessDayRecord> CloseBusinessDayAsync(DateOnly businessDate, DateTimeOffset closedAt, decimal totalRevenue, int totalOrders, int cancelledItems, int printFailures, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<BusinessDayRecord?> GetBusinessDayByDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<BusinessDayRecord?> GetActiveBusinessDayAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task RecordWaiterSummaryAsync(WaiterPerformanceRecord summary, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<WaiterPerformanceRecord>> GetWaiterSummariesByDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task RecordPrintErrorSummaryAsync(PrintErrorSummaryRecord summary, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PrintErrorSummaryRecord>> GetPrintErrorSummariesByDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
