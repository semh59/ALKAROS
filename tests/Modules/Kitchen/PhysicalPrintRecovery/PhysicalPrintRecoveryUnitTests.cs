namespace ALKAROS.Kitchen.PhysicalPrintRecovery.Tests;

using ALKAROS.Kitchen.PhysicalPrintRecovery;
using FluentAssertions;
using Xunit;

public sealed class PhysicalPrintRecoveryUnitTests
{
    private readonly Guid _printJobId = Guid.NewGuid();
    private readonly Guid _ticketId = Guid.NewGuid();
    private readonly Guid _printerId = Guid.NewGuid();
    private const string Payload = "MUTFAK SIPARIS FISI - TEST";

    [Fact]
    public void CreateInFlightInitializesDeliveryWithInFlightStatus()
    {
        var delivery = PhysicalPrintDelivery.CreateInFlight(_printJobId, _ticketId, _printerId, Payload);

        delivery.Id.Should().NotBeEmpty();
        delivery.PrintJobId.Should().Be(_printJobId);
        delivery.TicketId.Should().Be(_ticketId);
        delivery.PrinterId.Should().Be(_printerId);
        delivery.Status.Should().Be(PhysicalPrintDeliveryStatus.InFlight);
        delivery.AttemptNumber.Should().Be(1);
        delivery.IsReprint.Should().BeFalse();
        delivery.PayloadSnapshot.Should().Be(Payload);
        delivery.ReprintPayload.Should().BeNull();
        delivery.OperatorId.Should().BeNull();
        delivery.OperatorReason.Should().BeNull();
    }

    [Fact]
    public void MarkPrintedTransitionsInFlightToPrintedOnAck()
    {
        var delivery = PhysicalPrintDelivery.CreateInFlight(_printJobId, _ticketId, _printerId, Payload);
        var now = DateTimeOffset.UtcNow;

        var printed = delivery.MarkPrinted(now);

        printed.Status.Should().Be(PhysicalPrintDeliveryStatus.Printed);
        printed.DeliveredAt.Should().Be(now);
        printed.ResolvedAt.Should().Be(now);
    }

    [Fact]
    public void MarkUnknownTransitionsInFlightToUnknownOnCrashWindow()
    {
        var delivery = PhysicalPrintDelivery.CreateInFlight(_printJobId, _ticketId, _printerId, Payload);
        var now = DateTimeOffset.UtcNow;

        var unknown = delivery.MarkUnknown("Socket timeout during transmission", now);

        unknown.Status.Should().Be(PhysicalPrintDeliveryStatus.Unknown);
        unknown.CrashWindowReason.Should().Be("Socket timeout during transmission");
        unknown.DeliveredAt.Should().BeNull();
        unknown.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void UnknownStatusProhibitsDirectReprintWithoutOperatorApproval()
    {
        var delivery = PhysicalPrintDelivery.CreateInFlight(_printJobId, _ticketId, _printerId, Payload);
        var now = DateTimeOffset.UtcNow;
        var unknown = delivery.MarkUnknown("Connection dropped", now);

        // Attempting to mark reprinted directly from Unknown throws UnauthorizedReprintException
        var act = () => unknown.MarkReprinted(now);
        act.Should().Throw<UnauthorizedReprintException>();
    }

    [Fact]
    public void ApproveReprintAttachesReprintWatermarkBannerAndTransitionsToApproved()
    {
        var delivery = PhysicalPrintDelivery.CreateInFlight(_printJobId, _ticketId, _printerId, Payload);
        var now = DateTimeOffset.UtcNow;
        var unknown = delivery.MarkUnknown("Socket closed", now);

        var approved = unknown.ApproveReprint("Chef-Semih", "Yazicida kagit sikismisti, basilmadi", now);

        approved.Status.Should().Be(PhysicalPrintDeliveryStatus.ReprintApproved);
        approved.IsReprint.Should().BeTrue();
        approved.OperatorId.Should().Be("Chef-Semih");
        approved.OperatorReason.Should().Be("Yazicida kagit sikismisti, basilmadi");
        approved.ReprintPayload.Should().NotBeNull();
        approved.ReprintPayload.Should().Contain("*** TEKRAR BASKI / REPRINT ***");
        approved.ReprintPayload.Should().Contain("*** MUKERRER RISKLI KOPYA ***");
        approved.ReprintPayload.Should().Contain("ONAYLAYAN: Chef-Semih");
        approved.ReprintPayload.Should().Contain("NEDEN: Yazicida kagit sikismisti, basilmadi");
        approved.ReprintPayload.Should().Contain(Payload);
    }

    [Fact]
    public void RejectReprintDismissesReprintAndTransitionsToRejected()
    {
        var delivery = PhysicalPrintDelivery.CreateInFlight(_printJobId, _ticketId, _printerId, Payload);
        var now = DateTimeOffset.UtcNow;
        var unknown = delivery.MarkUnknown("Socket closed", now);

        var rejected = unknown.RejectReprint("Chef-Semih", "Fis mutfakta cikmis goruldu, tekrar basma", now);

        rejected.Status.Should().Be(PhysicalPrintDeliveryStatus.ReprintRejected);
        rejected.IsReprint.Should().BeFalse();
        rejected.OperatorId.Should().Be("Chef-Semih");
        rejected.OperatorReason.Should().Be("Fis mutfakta cikmis goruldu, tekrar basma");
        rejected.ReprintPayload.Should().BeNull();
    }

    [Fact]
    public void MarkReprintedTransitionsApprovedToReprinted()
    {
        var delivery = PhysicalPrintDelivery.CreateInFlight(_printJobId, _ticketId, _printerId, Payload);
        var now = DateTimeOffset.UtcNow;
        var unknown = delivery.MarkUnknown("Crash", now);
        var approved = unknown.ApproveReprint("Operator-1", "Onaylandi", now);

        var reprinted = approved.MarkReprinted(now.AddSeconds(5));

        reprinted.Status.Should().Be(PhysicalPrintDeliveryStatus.Reprinted);
        reprinted.IsReprint.Should().BeTrue();
        reprinted.DeliveredAt.Should().Be(now.AddSeconds(5));
    }

    [Fact]
    public void InvalidStateTransitionsThrowInvalidPhysicalPrintTransitionException()
    {
        var delivery = PhysicalPrintDelivery.CreateInFlight(_printJobId, _ticketId, _printerId, Payload);
        var now = DateTimeOffset.UtcNow;
        var printed = delivery.MarkPrinted(now);

        // Cannot transition from Printed to Unknown
        var actUnknown = () => printed.MarkUnknown("Too late", now);
        actUnknown.Should().Throw<InvalidPhysicalPrintTransitionException>();

        // Cannot approve reprint on already Printed delivery
        var actApprove = () => printed.ApproveReprint("Op1", "Reason", now);
        actApprove.Should().Throw<InvalidPhysicalPrintTransitionException>();
    }
}
