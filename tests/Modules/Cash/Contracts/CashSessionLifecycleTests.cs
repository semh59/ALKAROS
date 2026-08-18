using FluentAssertions;
using Xunit;

namespace ALKAROS.Cash.Contracts.Tests;

public sealed class CashSessionLifecycleTests
{
    [Fact]
    public void CashSessionSnapshotConstructsAndEvaluatesIsActiveCorrectly()
    {
        var sessionId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var openSession = new CashSessionSnapshot(
            sessionId,
            cashierId,
            terminalId,
            CashSessionStatus.Open,
            OpeningBalance: 500.00m,
            ExpectedCash: 500.00m,
            ActualCash: 0m,
            Difference: 0m,
            now,
            ClosedAt: null,
            RowVersion: 1);

        openSession.CashSessionId.Should().Be(sessionId);
        openSession.Status.Should().Be(CashSessionStatus.Open);
        openSession.OpeningBalance.Should().Be(500.00m);
        openSession.IsActive.Should().BeTrue();

        var countingSession = openSession with { Status = CashSessionStatus.Counting };
        countingSession.IsActive.Should().BeTrue();

        var closingSession = openSession with { Status = CashSessionStatus.Closing };
        closingSession.IsActive.Should().BeTrue();

        var closedSession = openSession with { Status = CashSessionStatus.Closed, ClosedAt = now };
        closedSession.IsActive.Should().BeFalse();

        var reconciledSession = openSession with { Status = CashSessionStatus.Reconciled, ClosedAt = now };
        reconciledSession.IsActive.Should().BeFalse();
    }

    [Fact]
    public void OpenCashSessionCommandValidation()
    {
        var valid = new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 250.00m);
        valid.Validate();

        var emptySession = new OpenCashSessionCommand(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 250.00m);
        var actEmptySession = () => emptySession.Validate();
        actEmptySession.Should().Throw<ArgumentException>().WithParameterName("CashSessionId");

        var emptyCashier = new OpenCashSessionCommand(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), 250.00m);
        var actEmptyCashier = () => emptyCashier.Validate();
        actEmptyCashier.Should().Throw<ArgumentException>().WithParameterName("CashierUserId");

        var emptyTerminal = new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, 250.00m);
        var actEmptyTerminal = () => emptyTerminal.Validate();
        actEmptyTerminal.Should().Throw<ArgumentException>().WithParameterName("TerminalId");

        var negativeOpening = new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), -10.00m);
        var actNegativeOpening = () => negativeOpening.Validate();
        var ex = actNegativeOpening.Should().Throw<NegativeCashAmountException>().Which;
        ex.ParameterName.Should().Be("OpeningBalance");
        ex.Amount.Should().Be(-10.00m);
    }

    [Fact]
    public void CloseCashSessionCommandValidation()
    {
        var valid = new CloseCashSessionCommand(Guid.NewGuid(), 1000.00m, Guid.NewGuid());
        valid.Validate();

        var negativeActual = new CloseCashSessionCommand(Guid.NewGuid(), -5.00m, Guid.NewGuid());
        var actNegativeActual = () => negativeActual.Validate();
        actNegativeActual.Should().Throw<NegativeCashAmountException>();

        var supervisorWithoutReason = new CloseCashSessionCommand(
            Guid.NewGuid(),
            1000.00m,
            Guid.NewGuid(),
            IsSupervisorOverride: true,
            OverrideReason: "  ");
        var actSupervisor = () => supervisorWithoutReason.Validate();
        actSupervisor.Should().Throw<ArgumentException>().WithParameterName("OverrideReason");
    }

    [Fact]
    public void DomainExceptionsCaptureParametersProperly()
    {
        var terminalId = Guid.NewGuid();
        var existingSessionId = Guid.NewGuid();

        var conflictEx = new ActiveCashSessionExistsException(terminalId, existingSessionId);
        conflictEx.TerminalId.Should().Be(terminalId);
        conflictEx.ExistingSessionId.Should().Be(existingSessionId);

        var stateEx = new InvalidCashSessionStateException(existingSessionId, CashSessionStatus.Closed, "StartCount");
        stateEx.SessionId.Should().Be(existingSessionId);
        stateEx.CurrentStatus.Should().Be(CashSessionStatus.Closed);
        stateEx.AttemptedAction.Should().Be("StartCount");

        var varianceEx = new CashVarianceThresholdExceededException(existingSessionId, 75.00m, 50.00m);
        varianceEx.SessionId.Should().Be(existingSessionId);
        varianceEx.Difference.Should().Be(75.00m);
        varianceEx.Threshold.Should().Be(50.00m);
    }
}
