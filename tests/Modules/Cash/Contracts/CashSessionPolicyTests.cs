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

    private static CashSessionSnapshot Session(CashSessionStatus status, Guid? cashier = null, decimal expected = 1000m)
        => new(Guid.NewGuid(), cashier ?? Guid.NewGuid(), Guid.NewGuid(), status, 200m, expected, 0m, 0m, DateTimeOffset.UtcNow, null, 3);

    private static CashSessionCloseInput Close(
        decimal actual, decimal expected = 1000m, decimal? counted = null, bool supervisor = false, decimal tolerance = 50m,
        string? explanation = null, Guid? actor = null, bool mayAct = false)
        => new(actual, expected, counted ?? actual, supervisor, tolerance, explanation, actor ?? Guid.Empty, mayAct);

    [Fact]
    public void StartCountPassesWhenSessionIsOpenAndFailsOtherwise()
    {
        var cashierId = Guid.NewGuid();
        var openSession = Session(CashSessionStatus.Open, cashierId);

        var actOpen = () => _policy.ValidateCanStartCount(openSession, cashierId, actorMayActForOwner: false);
        actOpen.Should().NotThrow();

        var closedSession = openSession with { Status = CashSessionStatus.Closed };
        var actClosed = () => _policy.ValidateCanStartCount(closedSession, cashierId, actorMayActForOwner: false);
        actClosed.Should().Throw<InvalidCashSessionStateException>();
    }

    [Fact]
    public void OnlyTheSessionsCashierOrAnActorWhoMayActForThemStartsCountsAndRecordsCounts()
    {
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var open = Session(CashSessionStatus.Open, owner);
        var counting = Session(CashSessionStatus.Counting, owner);

        var start = () => _policy.ValidateCanStartCount(open, stranger, actorMayActForOwner: false);
        start.Should().Throw<CashSessionOwnerRequiredException>().Which.ActorUserId.Should().Be(stranger);
        var record = () => _policy.ValidateCanRecordCount(counting, 10m, stranger, actorMayActForOwner: false);
        record.Should().Throw<CashSessionOwnerRequiredException>();

        _policy.ValidateCanStartCount(open, stranger, actorMayActForOwner: true);
        _policy.ValidateCanRecordCount(counting, 10m, stranger, actorMayActForOwner: true);
        _policy.ValidateCanRecordCount(counting, 10m, owner, actorMayActForOwner: false);
    }

    [Fact]
    public void AnOpenSessionCanNeitherRecordACountNorCloseBeforeCountingStarts()
    {
        // V1-RMD-417 (V1-RMD-393 F-15): cash-session-design.md section 5.
        var owner = Guid.NewGuid();
        var openSession = Session(CashSessionStatus.Open, owner, expected: 200m);

        var count = () => _policy.ValidateCanRecordCount(openSession, 200m, owner, actorMayActForOwner: false);
        count.Should().Throw<InvalidCashSessionStateException>();
        var close = () => _policy.ValidateCanCloseSession(openSession, Close(200m, 200m, actor: owner));
        close.Should().Throw<InvalidCashSessionStateException>();

        var counting = openSession with { Status = CashSessionStatus.Counting };
        _policy.ValidateCanCloseSession(counting, Close(200m, 200m, actor: owner)).Should().Be(0m);
    }

    [Fact]
    public void RecordCountValidatesAmountAndStatus()
    {
        var cashierId = Guid.NewGuid();
        var countingSession = Session(CashSessionStatus.Counting, cashierId, expected: 200m);

        var actValid = () => _policy.ValidateCanRecordCount(countingSession, 450.50m, cashierId, actorMayActForOwner: false);
        actValid.Should().NotThrow();

        var actNegative = () => _policy.ValidateCanRecordCount(countingSession, -1.00m, cashierId, actorMayActForOwner: false);
        actNegative.Should().Throw<NegativeCashAmountException>();

        var reconciledSession = countingSession with { Status = CashSessionStatus.Reconciled };
        var actReconciled = () => _policy.ValidateCanRecordCount(reconciledSession, 100m, cashierId, actorMayActForOwner: false);
        actReconciled.Should().Throw<InvalidCashSessionStateException>();
    }

    [Fact]
    public void CloseSessionWithinTolerancePassesWithoutSupervisorOverrideButNeedsAnExplanation()
    {
        var owner = Guid.NewGuid();
        var closingSession = Session(CashSessionStatus.Closing, owner);

        // Expected: 1000, Actual: 980 -> Difference: -20 (within 50.00 tolerance)
        var diff = _policy.ValidateCanCloseSession(closingSession, Close(980m, explanation: "para üstü", actor: owner));
        diff.Should().Be(-20.00m);

        var unexplained = () => _policy.ValidateCanCloseSession(closingSession, Close(980m, actor: owner));
        unexplained.Should().Throw<CashDifferenceExplanationRequiredException>().Which.Difference.Should().Be(-20m);
    }

    [Fact]
    public void CloseSessionExceedingToleranceRequiresSupervisorOverride()
    {
        var owner = Guid.NewGuid();
        var closingSession = Session(CashSessionStatus.Closing, owner);

        // Expected: 1000, Actual: 850 -> Difference: -150 (exceeds 50.00 tolerance)
        var actCashier = () => _policy.ValidateCanCloseSession(closingSession, Close(850m, explanation: "eksik", actor: owner));

        var ex = actCashier.Should().Throw<CashVarianceThresholdExceededException>().Which;
        ex.Difference.Should().Be(-150.00m);
        ex.Threshold.Should().Be(50.00m);

        var supervisorDiff = _policy.ValidateCanCloseSession(
            closingSession, Close(850m, supervisor: true, explanation: "eksik", actor: owner));
        supervisorDiff.Should().Be(-150.00m);
    }

    [Theory]
    [InlineData(10, 990, true)]
    [InlineData(10, 989.99, false)]
    [InlineData(10, 1010, true)]
    [InlineData(10, 1010.01, false)]
    [InlineData(500, 500, true)]
    [InlineData(500, 499.99, false)]
    [InlineData(0, 999.99, false)]
    public void TheDifferenceEqualToTheToleranceClosesAndOneKurusMoreNeedsAnOverride(double tolerance, double actual, bool closes)
    {
        var owner = Guid.NewGuid();
        var session = Session(CashSessionStatus.Counting, owner);

        var close = () => _policy.ValidateCanCloseSession(
            session, Close((decimal)actual, tolerance: (decimal)tolerance, explanation: "açıklama", actor: owner));

        if (closes)
            close.Should().NotThrow();
        else
            close.Should().Throw<CashVarianceThresholdExceededException>();
    }

    [Fact]
    public void ANegativeToleranceIsRefused()
    {
        var owner = Guid.NewGuid();
        var close = () => _policy.ValidateCanCloseSession(
            Session(CashSessionStatus.Counting, owner), Close(1000m, tolerance: -1m, actor: owner));
        close.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AZeroDifferenceNeedsNoExplanationAndAnyOtherDifferenceDoes(string? explanation)
    {
        var owner = Guid.NewGuid();
        var session = Session(CashSessionStatus.Counting, owner);

        _policy.ValidateCanCloseSession(session, Close(1000m, explanation: explanation, actor: owner)).Should().Be(0m);
        var shortBy = () => _policy.ValidateCanCloseSession(session, Close(999.99m, explanation: explanation, actor: owner));
        shortBy.Should().Throw<CashDifferenceExplanationRequiredException>();
    }

    [Fact]
    public void AnExplanationOfExactlyTheLimitPassesAndOneCharacterMoreIsRefusedEvenWithoutADifference()
    {
        var owner = Guid.NewGuid();
        var session = Session(CashSessionStatus.Counting, owner);

        _policy.ValidateCanCloseSession(session, Close(990m, explanation: new string('x', 500), actor: owner)).Should().Be(-10m);
        var tooLong = () => _policy.ValidateCanCloseSession(session, Close(990m, explanation: new string('x', 501), actor: owner));
        tooLong.Should().Throw<CashDifferenceExplanationRequiredException>();
        var tooLongNoDifference = () => _policy.ValidateCanCloseSession(session, Close(1000m, explanation: new string('x', 501), actor: owner));
        tooLongNoDifference.Should().Throw<CashDifferenceExplanationRequiredException>();
    }

    [Fact]
    public void TheClosingAmountMustEqualARecordedCount()
    {
        var owner = Guid.NewGuid();
        var session = Session(CashSessionStatus.Counting, owner);

        var none = () => _policy.ValidateCanCloseSession(session, Close(1000m, actor: owner) with { LatestCountedAmount = null });
        none.Should().Throw<CashCountRequiredException>();
        var different = () => _policy.ValidateCanCloseSession(session, Close(1000m, counted: 999m, actor: owner));
        var mismatch = different.Should().Throw<CashCountMismatchException>().Which;
        mismatch.Recorded.Should().Be(999m);
        mismatch.ClosingAmount.Should().Be(1000m);
    }

    [Fact]
    public void OnlyTheSessionsCashierOrAnActorWhoMayActForThemCloses()
    {
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var session = Session(CashSessionStatus.Counting, owner);

        var refused = () => _policy.ValidateCanCloseSession(session, Close(1000m, actor: stranger));
        refused.Should().Throw<CashSessionOwnerRequiredException>();
        _policy.ValidateCanCloseSession(session, Close(1000m, actor: stranger, mayAct: true)).Should().Be(0m);
    }

    [Fact]
    public void ReconcilePassesOnlyOnClosedSession()
    {
        var closedSession = Session(CashSessionStatus.Closed);
        var reconciler = Guid.NewGuid();

        var actClosed = () => _policy.ValidateCanReconcile(closedSession, [Guid.NewGuid()], reconciler, selfReconcileAllowed: false);
        actClosed.Should().NotThrow();

        var openSession = closedSession with { Status = CashSessionStatus.Open };
        var actOpen = () => _policy.ValidateCanReconcile(openSession, [], reconciler, selfReconcileAllowed: false);
        actOpen.Should().Throw<InvalidCashSessionStateException>();
    }

    [Fact]
    public void TheCashierTheCloserAndACounterReconcileOnlyWhenSelfReconciliationIsAllowedAndThenItIsFlagged()
    {
        var cashier = Guid.NewGuid();
        var closer = Guid.NewGuid();
        var counter = Guid.NewGuid();
        var third = Guid.NewGuid();
        var session = Session(CashSessionStatus.Closed, cashier);

        foreach (var self in new[] { cashier, closer, counter })
        {
            var refused = () => _policy.ValidateCanReconcile(session, [closer, counter], self, selfReconcileAllowed: false);
            refused.Should().Throw<CashReconcileSelfNotAllowedException>().Which.ReconcilerUserId.Should().Be(self);
            _policy.ValidateCanReconcile(session, [closer, counter], self, selfReconcileAllowed: true).Should().BeTrue();
        }

        _policy.ValidateCanReconcile(session, [closer, counter], third, selfReconcileAllowed: false).Should().BeFalse();
        _policy.ValidateCanReconcile(session, [closer, counter], third, selfReconcileAllowed: true).Should().BeFalse();
    }
}
