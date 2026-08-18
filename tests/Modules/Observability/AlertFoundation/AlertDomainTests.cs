using FluentAssertions;
using Xunit;

namespace ALKAROS.Observability.AlertFoundation.Tests;

public sealed class AlertDomainTests
{
    [Fact]
    public void AlertRecordConstructsAndComputesIsActiveCorrectly()
    {
        var alertId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var openAlert = new AlertRecord(
            alertId,
            "PrinterPaperLow",
            AlertSeverity.Warning,
            AlertStatus.Open,
            "Kitchen Printer Paper Low",
            "Roll has less than 10% remaining.",
            DeduplicationKey: "PRN-KIT-01-PAPER",
            SourceReferenceType: "Printer",
            SourceReferenceId: Guid.NewGuid(),
            now,
            AcknowledgedAt: null,
            AcknowledgedBy: null,
            ResolvedAt: null,
            ResolvedBy: null,
            ResolutionReason: null,
            RowVersion: 1);

        openAlert.AlertId.Should().Be(alertId);
        openAlert.AlertType.Should().Be("PrinterPaperLow");
        openAlert.Severity.Should().Be(AlertSeverity.Warning);
        openAlert.Status.Should().Be(AlertStatus.Open);
        openAlert.IsActive.Should().BeTrue();
        openAlert.DeduplicationKey.Should().Be("PRN-KIT-01-PAPER");

        var ackAlert = openAlert with { Status = AlertStatus.Acknowledged };
        ackAlert.IsActive.Should().BeTrue();

        var escAlert = openAlert with { Status = AlertStatus.Escalated };
        escAlert.IsActive.Should().BeTrue();

        var supAlert = openAlert with { Status = AlertStatus.Suppressed };
        supAlert.IsActive.Should().BeFalse();

        var resAlert = openAlert with { Status = AlertStatus.Resolved };
        resAlert.IsActive.Should().BeFalse();
    }

    [Fact]
    public void RaiseAlertRequestValidation()
    {
        var valid = new RaiseAlertRequest("PaymentFailure", AlertSeverity.Critical, "Card Decline", "Timeout");
        valid.Validate();

        var emptyType = new RaiseAlertRequest("", AlertSeverity.Critical, "Title", "Message");
        var actEmptyType = () => emptyType.Validate();
        actEmptyType.Should().Throw<ArgumentException>().WithParameterName("AlertType");

        var emptyTitle = new RaiseAlertRequest("Type", AlertSeverity.Critical, "", "Message");
        var actEmptyTitle = () => emptyTitle.Validate();
        actEmptyTitle.Should().Throw<ArgumentException>().WithParameterName("Title");

        var emptyMessage = new RaiseAlertRequest("Type", AlertSeverity.Critical, "Title", " ");
        var actEmptyMessage = () => emptyMessage.Validate();
        actEmptyMessage.Should().Throw<ArgumentException>().WithParameterName("Message");
    }

    [Fact]
    public void AcknowledgeAlertRequestValidation()
    {
        var valid = new AcknowledgeAlertRequest(Guid.NewGuid(), 1, Guid.NewGuid());
        valid.Validate();

        var emptyAlert = new AcknowledgeAlertRequest(Guid.Empty, 1, Guid.NewGuid());
        var actEmptyAlert = () => emptyAlert.Validate();
        actEmptyAlert.Should().Throw<ArgumentException>().WithParameterName("AlertId");

        var emptyUser = new AcknowledgeAlertRequest(Guid.NewGuid(), 1, Guid.Empty);
        var actEmptyUser = () => emptyUser.Validate();
        actEmptyUser.Should().Throw<ArgumentException>().WithParameterName("AcknowledgedBy");

        var invalidVersion = new AcknowledgeAlertRequest(Guid.NewGuid(), 0, Guid.NewGuid());
        var actInvalidVersion = () => invalidVersion.Validate();
        actInvalidVersion.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("ExpectedRowVersion");
    }

    [Fact]
    public void ResolveAlertRequestValidation()
    {
        var valid = new ResolveAlertRequest(Guid.NewGuid(), 1, Guid.NewGuid(), "Replaced paper roll.");
        valid.Validate();

        var emptyReason = new ResolveAlertRequest(Guid.NewGuid(), 1, Guid.NewGuid(), "   ");
        var actEmptyReason = () => emptyReason.Validate();
        actEmptyReason.Should().Throw<ArgumentException>().WithParameterName("ResolutionReason");
    }

    [Fact]
    public void AlertExceptionsCapturePropertiesCorrectly()
    {
        var alertId = Guid.NewGuid();

        var notFound = new AlertNotFoundException(alertId);
        notFound.AlertId.Should().Be(alertId);

        var stateEx = new InvalidAlertStateException(alertId, AlertStatus.Resolved, "Acknowledge");
        stateEx.AlertId.Should().Be(alertId);
        stateEx.CurrentStatus.Should().Be(AlertStatus.Resolved);
        stateEx.AttemptedAction.Should().Be("Acknowledge");

        var concEx = new AlertConcurrencyException(alertId, 1, 2);
        concEx.AlertId.Should().Be(alertId);
        concEx.ExpectedVersion.Should().Be(1);
        concEx.ActualVersion.Should().Be(2);
    }
}
