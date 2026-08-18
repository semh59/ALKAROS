using FluentAssertions;
using Xunit;

namespace ALKAROS.Tables.CurrentPointers.Tests;

public sealed class TablePointerDomainTests
{
    [Fact]
    public void DiscrepancyWithNoDriftFlagsReportsHasDriftFalse()
    {
        var tableId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var billId = Guid.NewGuid();

        var discrepancy = new TablePointerDiscrepancy(
            tableId,
            "T-10",
            "Occupied",
            "Occupied",
            orderId,
            orderId,
            billId,
            billId,
            TablePointerDriftType.None);

        discrepancy.TableId.Should().Be(tableId);
        discrepancy.TableNumber.Should().Be("T-10");
        discrepancy.CurrentStatus.Should().Be("Occupied");
        discrepancy.ProjectedStatus.Should().Be("Occupied");
        discrepancy.CurrentOrderId.Should().Be(orderId);
        discrepancy.AuthoritativeOrderId.Should().Be(orderId);
        discrepancy.CurrentBillId.Should().Be(billId);
        discrepancy.AuthoritativeBillId.Should().Be(billId);
        discrepancy.DriftTypes.Should().Be(TablePointerDriftType.None);
        discrepancy.HasDrift.Should().BeFalse();
    }

    [Theory]
    [InlineData(TablePointerDriftType.MissingOrderPointer)]
    [InlineData(TablePointerDriftType.StaleOrderPointer)]
    [InlineData(TablePointerDriftType.MissingBillPointer)]
    [InlineData(TablePointerDriftType.StaleBillPointer)]
    [InlineData(TablePointerDriftType.StatusMismatch)]
    [InlineData(TablePointerDriftType.GhostOrderPointer)]
    [InlineData(TablePointerDriftType.GhostBillPointer)]
    [InlineData(TablePointerDriftType.StatusMismatch | TablePointerDriftType.StaleOrderPointer)]
    public void DiscrepancyWithDriftFlagsReportsHasDriftTrue(TablePointerDriftType driftFlags)
    {
        var discrepancy = new TablePointerDiscrepancy(
            Guid.NewGuid(),
            "T-20",
            "Available",
            "Occupied",
            null,
            Guid.NewGuid(),
            null,
            null,
            driftFlags);

        discrepancy.HasDrift.Should().BeTrue();
        discrepancy.DriftTypes.Should().Be(driftFlags);
    }

    [Fact]
    public void RebuildResultCapturesValuesCorrectly()
    {
        var tableId = Guid.NewGuid();
        var prevOrderId = Guid.NewGuid();
        var newOrderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var result = new TablePointerRebuildResult(
            tableId,
            "T-30",
            "Available",
            "Occupied",
            prevOrderId,
            newOrderId,
            null,
            null,
            1,
            2,
            TablePointerDriftType.StatusMismatch | TablePointerDriftType.StaleOrderPointer,
            WasModified: true,
            now);

        result.TableId.Should().Be(tableId);
        result.TableNumber.Should().Be("T-30");
        result.PreviousStatus.Should().Be("Available");
        result.NewStatus.Should().Be("Occupied");
        result.PreviousOrderId.Should().Be(prevOrderId);
        result.NewOrderId.Should().Be(newOrderId);
        result.PreviousRowVersion.Should().Be(1);
        result.NewRowVersion.Should().Be(2);
        result.WasModified.Should().BeTrue();
        result.RebuiltAt.Should().Be(now);
    }

    [Fact]
    public void RebuildSummaryCalculatesMetricsCorrectly()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddSeconds(2);

        var summary = new TablePointerRebuildSummary(
            TotalScannedTables: 10,
            DriftedTablesCount: 2,
            RebuiltTablesCount: 2,
            Results: Array.Empty<TablePointerRebuildResult>(),
            DetectedDiscrepancies: Array.Empty<TablePointerDiscrepancy>(),
            start,
            end);

        summary.TotalScannedTables.Should().Be(10);
        summary.DriftedTablesCount.Should().Be(2);
        summary.RebuiltTablesCount.Should().Be(2);
        summary.AllClean.Should().BeFalse();
        summary.Duration.Should().Be(TimeSpan.FromSeconds(2));
    }
}
