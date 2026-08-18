using FluentAssertions;
using Xunit;

namespace ALKAROS.Tables.TableTransfer.Tests;

public sealed class TableTransferDomainTests
{
    [Fact]
    public void TableTransferRecordValidArgumentsConstructsCorrectly()
    {
        var id = Guid.NewGuid();
        var sourceTableId = Guid.NewGuid();
        var targetTableId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var transferredBy = Guid.NewGuid();
        var transferredAt = DateTimeOffset.UtcNow;
        const string reason = "Customer requested window seat.";

        var record = new TableTransferRecord(
            id,
            sourceTableId,
            targetTableId,
            orderId,
            billId,
            reason,
            transferredBy,
            transferredAt);

        record.Id.Should().Be(id);
        record.SourceTableId.Should().Be(sourceTableId);
        record.TargetTableId.Should().Be(targetTableId);
        record.OrderId.Should().Be(orderId);
        record.BillId.Should().Be(billId);
        record.Reason.Should().Be(reason);
        record.TransferredBy.Should().Be(transferredBy);
        record.TransferredAt.Should().Be(transferredAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TableTransferRecordEmptyReasonThrowsArgumentException(string invalidReason)
    {
        var act = () => new TableTransferRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            invalidReason,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("reason");
    }

    [Fact]
    public void TableTransferRecordEmptyIdThrowsArgumentException()
    {
        var act = () => new TableTransferRecord(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            "Transfer",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("id");
    }

    [Fact]
    public void TableTransferRecordSameSourceAndTargetThrowsArgumentException()
    {
        var sameId = Guid.NewGuid();
        var act = () => new TableTransferRecord(
            Guid.NewGuid(),
            sameId,
            sameId,
            null,
            null,
            "Transfer",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("targetTableId");
    }

    [Fact]
    public void TableTransferRecordEmptyTransferredByThrowsArgumentException()
    {
        var act = () => new TableTransferRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            "Transfer",
            Guid.Empty,
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("transferredBy");
    }

    [Fact]
    public void TableTransferRequestSameSourceAndTargetThrowsSameTableTransferException()
    {
        var sameId = Guid.NewGuid();
        var request = new TableTransferRequest(
            sameId,
            1,
            sameId,
            1,
            "Customer moved",
            Guid.NewGuid());

        var act = () => request.Validate();

        act.Should().Throw<SameTableTransferException>()
            .Which.TableId.Should().Be(sameId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TableTransferRequestInvalidSourceRowVersionThrowsArgumentOutOfRangeException(long invalidVersion)
    {
        var request = new TableTransferRequest(
            Guid.NewGuid(),
            invalidVersion,
            Guid.NewGuid(),
            1,
            "Reason",
            Guid.NewGuid());

        var act = () => request.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("ExpectedSourceRowVersion");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TableTransferRequestInvalidTargetRowVersionThrowsArgumentOutOfRangeException(long invalidVersion)
    {
        var request = new TableTransferRequest(
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            invalidVersion,
            "Reason",
            Guid.NewGuid());

        var act = () => request.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("ExpectedTargetRowVersion");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void TableTransferRequestEmptyReasonThrowsArgumentException(string invalidReason)
    {
        var request = new TableTransferRequest(
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            1,
            invalidReason,
            Guid.NewGuid());

        var act = () => request.Validate();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("Reason");
    }

    [Fact]
    public void TableTransferRequestEmptyTransferredByThrowsArgumentException()
    {
        var request = new TableTransferRequest(
            Guid.NewGuid(),
            1,
            Guid.NewGuid(),
            1,
            "Reason",
            Guid.Empty);

        var act = () => request.Validate();

        act.Should().Throw<ArgumentException>()
            .WithParameterName("TransferredBy");
    }

    [Fact]
    public void ExceptionsCaptureDomainPropertiesCorrectly()
    {
        var tableId = Guid.NewGuid();
        var sameEx = new SameTableTransferException(tableId);
        sameEx.TableId.Should().Be(tableId);
        sameEx.Should().BeAssignableTo<TableTransferException>();

        var notFoundEx = new TableNotFoundException(tableId, "Not found");
        notFoundEx.TableId.Should().Be(tableId);

        var sourceStateEx = new InvalidSourceTableStateException(tableId, "Available");
        sourceStateEx.TableId.Should().Be(tableId);
        sourceStateEx.ActualState.Should().Be("Available");

        var targetStateEx = new InvalidTargetTableStateException(tableId, "Occupied");
        targetStateEx.TableId.Should().Be(tableId);
        targetStateEx.ActualState.Should().Be("Occupied");

        var billId = Guid.NewGuid();
        var paymentPolicyEx = new PaymentPolicyRequiredException(billId, "Partially paid");
        paymentPolicyEx.BillId.Should().Be(billId);

        var concurrencyEx = new TableTransferConcurrencyException(tableId, 1, 2);
        concurrencyEx.TableId.Should().Be(tableId);
        concurrencyEx.ExpectedVersion.Should().Be(1);
        concurrencyEx.ActualVersion.Should().Be(2);
    }
}
