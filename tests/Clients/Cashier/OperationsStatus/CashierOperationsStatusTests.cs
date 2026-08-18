using FluentAssertions;
using Xunit;

namespace ALKAROS.Clients.Cashier.OperationsStatus.Tests;

public sealed class CashierOperationsStatusTests
{
    private readonly OperationsStatusEngine _engine = new();

    [Fact]
    public void LinkedOrderBillStatusComputesRemainingAndProgressAccurately()
    {
        var orderId = Guid.NewGuid();
        var billId = Guid.NewGuid();

        var printJob = new PrintJobStatusView(
            PrintJobId: Guid.NewGuid(),
            StationName: "Mutfak 1",
            Status: "Failed",
            RetryCount: 3,
            LastError: "Paper out",
            CanReprint: true);

        var ticket = new KitchenTicketProgressView(
            TicketId: Guid.NewGuid(),
            StationName: "Sıcak Mutfak",
            TicketStatus: "InPrep",
            TotalItems: 4,
            ReadyItems: 2,
            PrintJobs: new[] { printJob });

        var orderBill = new OrderBillStatusView(
            OrderId: orderId,
            BillId: billId,
            TableNumber: "M-08",
            BillTotal: 650.00m,
            PaidAmount: 250.00m,
            BillStatus: "PartiallyPaid",
            Tickets: new[] { ticket });

        _engine.LoadOperations(new[] { orderBill });

        var found = _engine.GetOrderByTable("M-08");
        found.Should().NotBeNull();
        found!.RemainingAmount.Should().Be(400.00m);
        found.IsFullyPaid.Should().BeFalse();
        found.Tickets[0].ReadyItems.Should().Be(2);
        found.Tickets[0].PrintJobs[0].LastError.Should().Be("Paper out");
    }

    [Fact]
    public void ReprintWithoutPermissionFailsWithExplicitError()
    {
        var command = new RequestReprintCommand(
            PrintJobId: Guid.NewGuid(),
            OperatorId: Guid.NewGuid(),
            Reason: "Yazıcı kağıdı bitti",
            HasReprintPermission: false);

        var success = _engine.ValidateAndExecuteReprint(command, out var error);

        success.Should().BeFalse();
        error.Should().Contain("yetkiniz bulunmamaktadır");
    }

    [Fact]
    public void ReprintWithoutReasonFailsWithExplicitError()
    {
        var command = new RequestReprintCommand(
            PrintJobId: Guid.NewGuid(),
            OperatorId: Guid.NewGuid(),
            Reason: "   ", // Empty reason
            HasReprintPermission: true);

        var success = _engine.ValidateAndExecuteReprint(command, out var error);

        success.Should().BeFalse();
        error.Should().Contain("gerekçesi (nedeni) girilmesi zorunludur");
    }

    [Fact]
    public void AuthorizedReprintWithReasonSucceeds()
    {
        var command = new RequestReprintCommand(
            PrintJobId: Guid.NewGuid(),
            OperatorId: Guid.NewGuid(),
            Reason: "Yazıcı rulosu değişti, mutfak fişi tekrar istendi",
            HasReprintPermission: true);

        var success = _engine.ValidateAndExecuteReprint(command, out var error);

        success.Should().BeTrue();
        error.Should().BeNull();
        _engine.ErrorMessage.Should().BeNull();
    }
}
