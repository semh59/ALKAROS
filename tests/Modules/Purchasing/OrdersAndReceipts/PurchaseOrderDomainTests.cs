using System;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Purchasing.OrdersAndReceipts.Tests;

public sealed class PurchaseOrderDomainTests
{
    [Fact]
    public void CreatePurchaseOrderWithValidDetailsSucceeds()
    {
        var supplierId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var po = PurchaseOrder.Create(" po-2026-001 ", supplierId, locationId, "Weekly vegetables");

        po.OrderNumber.Should().Be("PO-2026-001");
        po.SupplierId.Should().Be(supplierId);
        po.DestinationLocationId.Should().Be(locationId);
        po.Status.Should().Be(PurchaseOrderStatus.Draft);
        po.Notes.Should().Be("Weekly vegetables");
        po.Currency.Should().Be("TRY");
        po.TotalAmount.Should().Be(0m);
    }

    [Fact]
    public void AddLineRecalculatesTotalAmount()
    {
        var po = PurchaseOrder.Create("PO-01", Guid.NewGuid(), Guid.NewGuid());
        po.AddLine(Guid.NewGuid(), 10m, "kg", 25.50m);
        po.AddLine(Guid.NewGuid(), 5m, "l", 50.00m);

        po.Lines.Should().HaveCount(2);
        po.TotalAmount.Should().Be(505.00m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AddLineWithInvalidQuantityThrowsInvalidPurchaseOrderException(decimal qty)
    {
        var po = PurchaseOrder.Create("PO-01", Guid.NewGuid(), Guid.NewGuid());
        var act = () => po.AddLine(Guid.NewGuid(), qty, "kg", 10m);

        act.Should().Throw<InvalidPurchaseOrderException>()
            .WithMessage("*greater than zero*");
    }

    [Fact]
    public void SubmitWithoutLinesThrowsInvalidPurchaseOrderException()
    {
        var po = PurchaseOrder.Create("PO-01", Guid.NewGuid(), Guid.NewGuid());
        var act = () => po.Submit();

        act.Should().Throw<InvalidPurchaseOrderException>()
            .WithMessage("*without line items*");
    }

    [Fact]
    public void SubmitWithLinesTransitionsToSubmitted()
    {
        var po = PurchaseOrder.Create("PO-01", Guid.NewGuid(), Guid.NewGuid());
        po.AddLine(Guid.NewGuid(), 10m, "kg", 15m);
        po.Submit();

        po.Status.Should().Be(PurchaseOrderStatus.Submitted);
    }

    [Fact]
    public void CancelTransitionsDraftOrSubmittedToCancelled()
    {
        var po = PurchaseOrder.Create("PO-01", Guid.NewGuid(), Guid.NewGuid());
        po.Cancel();
        po.Status.Should().Be(PurchaseOrderStatus.Cancelled);
    }

    [Fact]
    public void RecordReceiptUpdatesLineAndOrderStatus()
    {
        var po = PurchaseOrder.Create("PO-01", Guid.NewGuid(), Guid.NewGuid());
        po.AddLine(Guid.NewGuid(), 10m, "kg", 20m);
        po.Submit();

        var line = po.Lines[0];
        line.RecordReceived(4m);
        line.ReceivedQuantity.Should().Be(4m);
        line.Status.Should().Be(PurchaseOrderLineStatus.PartiallyReceived);

        po.UpdateStatusFromLines();
        po.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);

        line.RecordReceived(6m);
        line.ReceivedQuantity.Should().Be(10m);
        line.Status.Should().Be(PurchaseOrderLineStatus.Completed);

        po.UpdateStatusFromLines();
        po.Status.Should().Be(PurchaseOrderStatus.Completed);
    }
}
