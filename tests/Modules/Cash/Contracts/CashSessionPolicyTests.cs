using FluentAssertions;
using Xunit;

namespace ALKAROS.Cash.Contracts.Tests;

public sealed class CashSessionPolicyTests
{
    private readonly CashSessionPolicy _policy = new();

    [Fact]
    public void OpenSessionPassesWhenNoActiveSessionExistsOnTerminal()
    {
        var terminalId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();

        var closedPastSession = new CashSessionSnapshot(
            Guid.NewGuid(),
            cashierId,
            terminalId,
            CashSessionStatus.Closed,
            200m, 200m, 200m, 0m,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(-1).AddHours(8),
            RowVersion: 5);

        var cmd = new OpenCashSessionCommand(Guid.NewGuid(), cashierId, terminalId, 300.00m);

        var act = () => _policy.ValidateCanOpenSession(cmd, new[] { closedPastSession });
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(CashSessionStatus.Open)]
    [InlineData(CashSessionStatus.Counting)]
    [InlineData(CashSessionStatus.Closing)]
    public void OpenSessionThrowsWhenTerminalHasExistingActiveSession(CashSessionStatus activeStatus)
    {
        var terminalId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();
        var existingSessionId = Guid.NewGuid();

        var activeSession = new CashSessionSnapshot(
            existingSessionId,
            cashierId,
            terminalId,
            activeStatus,
            200m, 200m, 0m, 0m,
            DateTimeOffset.UtcNow.AddHours(-2),
            ClosedAt: null,
            RowVersion: 1);

        var cmd = new OpenCashSessionCommand(Guid.NewGuid(), cashierId, terminalId, 300.00m);

        var act = () => _policy.ValidateCanOpenSession(cmd, new[] { activeSession });
        var ex = act.Should().Throw<ActiveCashSessionExistsException>().Which;
        ex.TerminalId.Should().Be(terminalId);
        ex.ExistingSessionId.Should().Be(existingSessionId);
    }

    [Fact]
    public void StartCountPassesWhenSessionIsOpenAndFailsOtherwise()
    {
        var sessionId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();

        var openSession = new CashSessionSnapshot(
            sessionId, cashierId, Guid.NewGuid(), CashSessionStatus.Open,
            200m, 200m, 0m, 0m, DateTimeOffset.UtcNow, null, 1);

        var actOpen = () => _policy.ValidateCanStartCount(openSession, cashierId);
        actOpen.Should().NotThrow();

        var closedSession = openSession with { Status = CashSessionStatus.Closed };
        var actClosed = () => _policy.ValidateCanStartCount(closedSession, cashierId);
        actClosed.Should().Throw<InvalidCashSessionStateException>();
    }

    [Fact]
    public void RecordCountValidatesAmountAndStatus()
    {
        var sessionId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();

        var countingSession = new CashSessionSnapshot(
            sessionId, cashierId, Guid.NewGuid(), CashSessionStatus.Counting,
            200m, 200m, 0m, 0m, DateTimeOffset.UtcNow, null, 2);

        var actValid = () => _policy.ValidateCanRecordCount(countingSession, 450.50m);
        actValid.Should().NotThrow();

        var actNegative = () => _policy.ValidateCanRecordCount(countingSession, -1.00m);
        actNegative.Should().Throw<NegativeCashAmountException>();

        var reconciledSession = countingSession with { Status = CashSessionStatus.Reconciled };
        var actReconciled = () => _policy.ValidateCanRecordCount(reconciledSession, 100m);
        actReconciled.Should().Throw<InvalidCashSessionStateException>();
    }

    [Fact]
    public void CloseSessionWithinTolerancePassesWithoutSupervisorOverride()
    {
        var sessionId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();

        var closingSession = new CashSessionSnapshot(
            sessionId, cashierId, Guid.NewGuid(), CashSessionStatus.Closing,
            200m, ExpectedCash: 1000.00m, ActualCash: 0m, Difference: 0m,
            DateTimeOffset.UtcNow, null, 3);

        // Expected: 1000, Actual: 980 -> Difference: -20 (within 50.00 tolerance)
        var diff = _policy.ValidateCanCloseSession(
            closingSession,
            actualCash: 980.00m,
            expectedCash: 1000.00m,
            isSupervisorOverride: false,
            varianceTolerance: 50.00m);

        diff.Should().Be(-20.00m);
    }

    [Fact]
    public void CloseSessionExceedingToleranceRequiresSupervisorOverride()
    {
        var sessionId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();

        var closingSession = new CashSessionSnapshot(
            sessionId, cashierId, Guid.NewGuid(), CashSessionStatus.Closing,
            200m, ExpectedCash: 1000.00m, ActualCash: 0m, Difference: 0m,
            DateTimeOffset.UtcNow, null, 3);

        // Expected: 1000, Actual: 850 -> Difference: -150 (exceeds 50.00 tolerance)
        var actCashier = () => _policy.ValidateCanCloseSession(
            closingSession,
            actualCash: 850.00m,
            expectedCash: 1000.00m,
            isSupervisorOverride: false,
            varianceTolerance: 50.00m);

        var ex = actCashier.Should().Throw<CashVarianceThresholdExceededException>().Which;
        ex.Difference.Should().Be(-150.00m);
        ex.Threshold.Should().Be(50.00m);

        // With Supervisor Override -> Passes and returns variance
        var supervisorDiff = _policy.ValidateCanCloseSession(
            closingSession,
            actualCash: 850.00m,
            expectedCash: 1000.00m,
            isSupervisorOverride: true,
            varianceTolerance: 50.00m);

        supervisorDiff.Should().Be(-150.00m);
    }

    [Fact]
    public void ReconcilePassesOnlyOnClosedSession()
    {
        var sessionId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();

        var closedSession = new CashSessionSnapshot(
            sessionId, cashierId, Guid.NewGuid(), CashSessionStatus.Closed,
            200m, 1000m, 1000m, 0m, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 4);

        var actClosed = () => _policy.ValidateCanReconcile(closedSession);
        actClosed.Should().NotThrow();

        var openSession = closedSession with { Status = CashSessionStatus.Open };
        var actOpen = () => _policy.ValidateCanReconcile(openSession);
        actOpen.Should().Throw<InvalidCashSessionStateException>();
    }
}
