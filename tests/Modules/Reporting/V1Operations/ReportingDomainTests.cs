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
        private readonly Dictionary<DateOnly, BusinessDayRecord> _businessDays = [];
        private readonly List<WaiterPerformanceRecord> _waiterSummaries = [];
        private readonly List<PrintErrorSummaryRecord> _printSummaries = [];

        public Task<BusinessDayRecord> OpenBusinessDayAsync(DateOnly businessDate, DateTimeOffset openedAt, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_businessDays.Values.Any(day => day.Status == BusinessDayStatus.Open))
                throw new InvalidOperationException("An active business day already exists.");

            var record = new BusinessDayRecord(Guid.NewGuid(), businessDate, openedAt, null, BusinessDayStatus.Open, 0m, 0, 0, 0);
            _businessDays.Add(businessDate, record);
            return Task.FromResult(record);
        }

        public Task<BusinessDayRecord> CloseBusinessDayAsync(DateOnly businessDate, DateTimeOffset closedAt, decimal totalRevenue, int totalOrders, int cancelledItems, int printFailures, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_businessDays.TryGetValue(businessDate, out var current) || current.Status != BusinessDayStatus.Open)
                throw new InvalidOperationException("An open business day was not found.");

            var closed = current with
            {
                ClosedAt = closedAt,
                Status = BusinessDayStatus.Closed,
                TotalRevenue = totalRevenue,
                TotalOrdersCount = totalOrders,
                TotalCancelledItemsCount = cancelledItems,
                TotalPrintFailuresCount = printFailures
            };
            _businessDays[businessDate] = closed;
            return Task.FromResult(closed);
        }

        public Task<BusinessDayRecord?> GetBusinessDayByDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _businessDays.TryGetValue(businessDate, out var record);
            return Task.FromResult(record);
        }

        public Task<BusinessDayRecord?> GetActiveBusinessDayAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_businessDays.Values.SingleOrDefault(day => day.Status == BusinessDayStatus.Open));
        }

        public Task RecordWaiterSummaryAsync(WaiterPerformanceRecord summary, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _waiterSummaries.Add(summary);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WaiterPerformanceRecord>> GetWaiterSummariesByDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<WaiterPerformanceRecord>>(_waiterSummaries.Where(summary => summary.BusinessDate == businessDate).ToList());
        }

        public Task RecordPrintErrorSummaryAsync(PrintErrorSummaryRecord summary, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _printSummaries.Add(summary);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PrintErrorSummaryRecord>> GetPrintErrorSummariesByDateAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<PrintErrorSummaryRecord>>(_printSummaries.Where(summary => summary.BusinessDate == businessDate).ToList());
        }
    }
}
