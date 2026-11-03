using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.SessionLifecycle.Tests.Fixtures;
using ALKAROS.Cash.TransactionLedger;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Cash.SessionLifecycle.Tests;

/// <summary>
/// Integration tests for CashSessionLifecycleService + PostgresCashSessionRepository
/// against real Postgres (V13-CSH-001). Exercises the full command set
/// (Open/StartCount/RecordCount/Close/Reconcile), the CSH-INV-01 single-
/// open-session invariant, and optimistic concurrency.
/// </summary>
public sealed class CashSessionLifecycleServiceTests : IClassFixture<CashSessionTestDatabase>
{
    private readonly PostgresCashSessionRepository _repository;
    private readonly PostgresCashTransactionLedgerRepository _ledger;
    private readonly Npgsql.NpgsqlDataSource _dataSource;
    private readonly CashSessionLifecycleService _service;

    public CashSessionLifecycleServiceTests(CashSessionTestDatabase database)
    {
        _dataSource = database.DataSource;
        _repository = new PostgresCashSessionRepository(database.DataSource);
        _ledger = new PostgresCashTransactionLedgerRepository(database.DataSource);
        _service = new CashSessionLifecycleService(_repository, new CashSessionPolicy());
    }

    /// <summary>A session opened with its opening entry, counting started and one count recorded, all by its own cashier.</summary>
    private async Task<(Guid SessionId, Guid Cashier)> CountedSessionAsync(decimal opening, decimal counted, Guid? terminalId = null)
    {
        var cashier = Guid.NewGuid();
        var command = new OpenCashSessionCommand(Guid.NewGuid(), cashier, terminalId ?? Guid.NewGuid(), opening);
        await _service.OpenSessionWithOpeningEntryAsync(command);
        await _service.StartCountAsync(new StartCashCountCommand(command.CashSessionId, cashier));
        await _service.RecordCountAsync(new RecordCashCountCommand(command.CashSessionId, counted, cashier));
        return (command.CashSessionId, cashier);
    }

    private Task<(CashSessionSnapshot Session, CashSessionClosedEvent Event)> CloseAsync(
        Guid sessionId, Guid by, decimal actual, string? explanation = null, decimal tolerance = 50m, bool supervisor = false,
        string? overrideReason = null, bool mayActForOwner = false,
        Func<CashSessionClosedEvent, ILockedCashSession, CancellationToken, Task>? inTransaction = null)
        => _service.CloseSessionAsync(
            new CloseCashSessionCommand(sessionId, actual, by, supervisor, overrideReason, explanation, mayActForOwner),
            tolerance, inTransaction);

    private Task<Guid> RecordCountAsync(Guid sessionId, decimal amount, Guid by, string? notes)
        => _repository.InLockedSessionAsync(sessionId, locked => locked.RecordCountAsync(amount, by, notes));

    [Fact]
    public async Task OpenSessionAsyncPersistsAndRoundTripsAnOpenSession()
    {
        var terminalId = Guid.NewGuid();
        var command = new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), terminalId, 500.00m);

        var (session, openedEvent) = await _service.OpenSessionAsync(command);

        session.Status.Should().Be(CashSessionStatus.Open);
        session.OpeningBalance.Should().Be(500.00m);
        session.ExpectedCash.Should().Be(500.00m);
        session.ActualCash.Should().Be(0m);
        session.RowVersion.Should().Be(1);
        openedEvent.TerminalId.Should().Be(terminalId);
        openedEvent.OpeningBalance.Should().Be(500.00m);

        var loaded = await _repository.GetByIdAsync(command.CashSessionId);
        loaded.Should().NotBeNull();
        loaded!.Snapshot.Status.Should().Be(CashSessionStatus.Open);
        loaded.Snapshot.TerminalId.Should().Be(terminalId);
        loaded.ClosedBy.Should().BeNull();
        loaded.DifferenceExplanation.Should().BeNull();
        loaded.ReconciledSelfApproved.Should().BeFalse();
    }

    [Fact]
    public async Task OpenSessionAsyncRejectsASecondConflictingSessionOnTheSameTerminal()
    {
        // Acceptance evidence: "Bir terminal ikinci bir çakışan oturumu açamaz."
        var terminalId = Guid.NewGuid();
        await _service.OpenSessionAsync(new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), terminalId, 100.00m));

        var act = () => _service.OpenSessionAsync(new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), terminalId, 200.00m));

        await act.Should().ThrowAsync<ActiveCashSessionExistsException>()
            .Where(ex => ex.TerminalId == terminalId);
    }

    [Fact]
    public async Task OpenSessionAsyncAllowsANewSessionOnceThePreviousOneIsClosed()
    {
        var terminalId = Guid.NewGuid();
        var (first, cashier) = await CountedSessionAsync(100.00m, 100.00m, terminalId);
        await CloseAsync(first, cashier, 100.00m);

        var second = new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), terminalId, 150.00m);
        var (session, _) = await _service.OpenSessionAsync(second);

        session.Status.Should().Be(CashSessionStatus.Open);
    }

    [Fact]
    public async Task TheActiveQueryReturnsOnlyTheActiveSessionNotTheClosedHistory()
    {
        var terminalId = Guid.NewGuid();
        (await _repository.GetActiveByTerminalIdAsync(terminalId)).Should().BeEmpty();

        var (first, cashier) = await CountedSessionAsync(100.00m, 100.00m, terminalId);
        (await _repository.GetActiveByTerminalIdAsync(terminalId)).Should().ContainSingle(s => s.Status == CashSessionStatus.Counting);
        await CloseAsync(first, cashier, 100.00m);
        (await _repository.GetActiveByTerminalIdAsync(terminalId)).Should().BeEmpty();

        var second = new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), terminalId, 150.00m);
        await _service.OpenSessionAsync(second);

        (await _repository.GetActiveByTerminalIdAsync(terminalId))
            .Should().ContainSingle(s => s.CashSessionId == second.CashSessionId && s.Status == CashSessionStatus.Open);
        await using (var closing = _dataSource.CreateCommand(
            "UPDATE cash.cash_sessions SET status = 'Closing' WHERE cash_session_id = @id;"))
        {
            closing.Parameters.AddWithValue("id", second.CashSessionId);
            await closing.ExecuteNonQueryAsync();
        }

        (await _repository.GetActiveByTerminalIdAsync(terminalId))
            .Should().ContainSingle(s => s.CashSessionId == second.CashSessionId && s.Status == CashSessionStatus.Closing);
    }

    [Fact]
    public async Task ConcurrentOpenSessionAsyncCallsOnTheSameTerminalNeverBothSucceed()
    {
        // The GetActiveByTerminalIdAsync check and the insert are not atomic; two
        // truly concurrent opens on the same terminal must still resolve
        // to exactly one winner (the DB's partial unique index, migration
        // 122), with the loser seeing the same domain exception a
        // sequential second call would.
        var terminalId = Guid.NewGuid();

        var results = await Task.WhenAll(
            TryOpenAsync(terminalId),
            TryOpenAsync(terminalId));

        results.Count(r => r).Should().Be(1, "exactly one concurrent open must win the race, never both or neither");
    }

    private async Task<bool> TryOpenAsync(Guid terminalId)
    {
        try
        {
            await _service.OpenSessionAsync(new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), terminalId, 100.00m));
            return true;
        }
        catch (ActiveCashSessionExistsException)
        {
            return false;
        }
    }

    [Fact]
    public async Task CloseSessionAsyncOnAnAlreadyClosedSessionFails()
    {
        // Acceptance evidence: "eski kapatma başarısız olur."
        var (sessionId, cashier) = await CountedSessionAsync(100.00m, 100.00m);
        await CloseAsync(sessionId, cashier, 100.00m);

        var act = () => CloseAsync(sessionId, cashier, 100.00m);

        await act.Should().ThrowAsync<InvalidCashSessionStateException>()
            .Where(ex => ex.CurrentStatus == CashSessionStatus.Closed);
    }

    [Fact]
    public async Task ClosedSessionCannotBeSilentlyReopenedByStartingANewCount()
    {
        // Acceptance evidence: "Kapalı sessizce yeniden açılamaz" — no
        // lifecycle command ever transitions a Closed session back toward
        // Open; StartCount (the only command that produces an Open-like
        // active state) must still refuse it.
        var (sessionId, cashier) = await CountedSessionAsync(100.00m, 100.00m);
        await CloseAsync(sessionId, cashier, 100.00m);

        var act = () => _service.StartCountAsync(new StartCashCountCommand(sessionId, cashier));

        await act.Should().ThrowAsync<InvalidCashSessionStateException>()
            .Where(ex => ex.CurrentStatus == CashSessionStatus.Closed);
    }

    [Fact]
    public async Task ACountAfterTheCloseIsRefused()
    {
        var (sessionId, cashier) = await CountedSessionAsync(100.00m, 100.00m);
        await CloseAsync(sessionId, cashier, 100.00m);

        var act = () => _service.RecordCountAsync(new RecordCashCountCommand(sessionId, 90m, cashier));

        await act.Should().ThrowAsync<InvalidCashSessionStateException>().Where(ex => ex.CurrentStatus == CashSessionStatus.Closed);
        (await _repository.GetCountsAsync(sessionId)).Should().ContainSingle();
    }

    private async Task<(Guid SessionId, Guid Counter)> CountingSessionAsync()
    {
        var cashier = Guid.NewGuid();
        var command = new OpenCashSessionCommand(Guid.NewGuid(), cashier, Guid.NewGuid(), 100.00m);
        await _service.OpenSessionAsync(command);
        await _service.StartCountAsync(new StartCashCountCommand(command.CashSessionId, cashier));
        return (command.CashSessionId, Guid.NewGuid());
    }

    [Fact]
    public async Task ARetriedCountRequestFindsTheRowTheFirstAttemptWroteInsteadOfAddingAnotherOne()
    {
        var (sessionId, counter) = await CountingSessionAsync();

        var first = await RecordCountAsync(sessionId, 95.00m, counter, "Sayim 1");
        var retry = await RecordCountAsync(sessionId, 95.00m, counter, "Sayim 1");

        retry.Should().Be(first);
        (await _repository.GetCountsAsync(sessionId)).Should().ContainSingle();
    }

    [Fact]
    public async Task ACountWithAnotherAmountCounterOrNoteIsAnotherCount()
    {
        var (sessionId, counter) = await CountingSessionAsync();

        await RecordCountAsync(sessionId, 95.00m, counter, null);
        await RecordCountAsync(sessionId, 96.00m, counter, null);
        await RecordCountAsync(sessionId, 95.00m, Guid.NewGuid(), null);
        await RecordCountAsync(sessionId, 95.00m, counter, "Ikinci sayim");

        (await _repository.GetCountsAsync(sessionId)).Should().HaveCount(4);
    }

    [Fact]
    public async Task AnOlderIdenticalCountDoesNotHideALaterDifferentOne()
    {
        var (sessionId, counter) = await CountingSessionAsync();

        await RecordCountAsync(sessionId, 100m, counter, null);
        await RecordCountAsync(sessionId, 120m, counter, null);
        await RecordCountAsync(sessionId, 100m, counter, null);

        (await _repository.GetCountsAsync(sessionId)).Should().HaveCount(3);
        await using var command = _dataSource.CreateCommand(
            "SELECT counted_amount FROM cash.cash_counts WHERE cash_session_id = @id ORDER BY counted_at DESC LIMIT 1;");
        command.Parameters.AddWithValue("id", sessionId);
        ((decimal)(await command.ExecuteScalarAsync())!).Should().Be(100m);
    }

    [Fact]
    public async Task TheSameCountAfterTheRetryWindowIsARecount()
    {
        var (sessionId, counter) = await CountingSessionAsync();
        var first = await RecordCountAsync(sessionId, 95.00m, counter, null);
        await using (var age = _dataSource.CreateCommand("UPDATE cash.cash_counts SET counted_at = counted_at - interval '31 seconds' WHERE cash_count_id = @id;"))
        {
            age.Parameters.AddWithValue("id", first);
            await age.ExecuteNonQueryAsync();
        }

        var again = await RecordCountAsync(sessionId, 95.00m, counter, null);

        again.Should().NotBe(first);
        (await _repository.GetCountsAsync(sessionId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task FullLifecycleOpenCountCloseReconcileReachesReconciledWithCorrectDifference()
    {
        var cashier = Guid.NewGuid();
        var command = new OpenCashSessionCommand(Guid.NewGuid(), cashier, Guid.NewGuid(), 200.00m);
        await _service.OpenSessionWithOpeningEntryAsync(command);

        var (counting, _) = await _service.StartCountAsync(new StartCashCountCommand(command.CashSessionId, cashier));
        counting.Status.Should().Be(CashSessionStatus.Counting);

        var countEvent = await _service.RecordCountAsync(
            new RecordCashCountCommand(command.CashSessionId, 195.00m, cashier, "Sayim 1"));
        countEvent.CountedAmount.Should().Be(195.00m);

        var (closed, closedEvent) = await CloseAsync(command.CashSessionId, cashier, 195.00m, explanation: " bozuk para ");

        closed.Status.Should().Be(CashSessionStatus.Closed);
        closed.ExpectedCash.Should().Be(200.00m);
        closed.Difference.Should().Be(-5.00m);
        closedEvent.Difference.Should().Be(-5.00m);
        closedEvent.ClosedBy.Should().Be(cashier);
        closedEvent.DifferenceExplanation.Should().Be("bozuk para");
        (await _repository.GetByIdAsync(command.CashSessionId))!.DifferenceExplanation.Should().Be("bozuk para");

        var reconciledBy = Guid.NewGuid();
        var (reconciled, reconciledEvent) = await _service.ReconcileSessionAsync(
            new ReconcileCashSessionCommand(command.CashSessionId, reconciledBy, "Gunluk mutabakat"));

        reconciled.Status.Should().Be(CashSessionStatus.Reconciled);
        reconciledEvent.ReconciledBy.Should().Be(reconciledBy);
        reconciledEvent.SelfApproved.Should().BeFalse();

        var counts = await _repository.GetCountsAsync(command.CashSessionId);
        counts.Should().ContainSingle(c => c.CountedAmount == 195.00m && c.Notes == "Sayim 1");
    }

    [Fact]
    public async Task CloseSessionAsyncBeyondVarianceToleranceRequiresSupervisorOverride()
    {
        var (sessionId, cashier) = await CountedSessionAsync(100.00m, 200.00m);

        // 200 vs expected 100 => difference 100, over the 50 tolerance.
        var withoutOverride = () => CloseAsync(sessionId, cashier, 200.00m, explanation: "fazla");
        await withoutOverride.Should().ThrowAsync<CashVarianceThresholdExceededException>();

        var wider = await CloseAsync(sessionId, cashier, 200.00m, explanation: "fazla", tolerance: 100m);
        wider.Session.Difference.Should().Be(100.00m, "the tolerance is the caller's (manager) setting, not a constant");
    }

    [Fact]
    public async Task ASupervisorOverrideClosesBeyondTheTolerance()
    {
        var (sessionId, cashier) = await CountedSessionAsync(100.00m, 200.00m);

        var (closed, _) = await CloseAsync(
            sessionId, cashier, 200.00m, explanation: "fazla", supervisor: true, overrideReason: "Sayim farki onaylandi");

        closed.Status.Should().Be(CashSessionStatus.Closed);
        closed.Difference.Should().Be(100.00m);
    }

    [Fact]
    public async Task TheCloseReadsTheExpectedCashFromTheLedgerAndPostsNoDifferenceWhenTheCountMatches()
    {
        var cashier = Guid.NewGuid();
        var command = new OpenCashSessionCommand(Guid.NewGuid(), cashier, Guid.NewGuid(), 100.00m);
        await _service.OpenSessionWithOpeningEntryAsync(command);
        await _ledger.RecordAsync(new CashTransaction(
            Guid.NewGuid(), command.CashSessionId, CashTransactionType.CashIn, 50m, CashTransactionDirection.In,
            notes: "ek para", recordedBy: cashier));
        await _service.StartCountAsync(new StartCashCountCommand(command.CashSessionId, cashier));
        await _service.RecordCountAsync(new RecordCashCountCommand(command.CashSessionId, 150m, cashier));

        var (closed, _) = await CloseAsync(command.CashSessionId, cashier, 150m);

        closed.ExpectedCash.Should().Be(150m);
        closed.Difference.Should().Be(0m);
        (await _ledger.GetBySessionIdAsync(command.CashSessionId)).Should().NotContain(t => t.Type == CashTransactionType.ClosingDifference);
    }

    [Theory]
    [InlineData(87.50, CashTransactionDirection.Out, 12.50)]
    [InlineData(130, CashTransactionDirection.In, 30)]
    public async Task ANonZeroDifferenceIsPostedToTheLedgerWithoutChangingTheExpectedCash(
        double counted, CashTransactionDirection direction, double amount)
    {
        var (sessionId, cashier) = await CountedSessionAsync(100.00m, (decimal)counted);

        await CloseAsync(sessionId, cashier, (decimal)counted, explanation: "açıklama", tolerance: 50m);

        var entries = await _ledger.GetBySessionIdAsync(sessionId);
        var difference = entries.Should().ContainSingle(t => t.Type == CashTransactionType.ClosingDifference).Subject;
        difference.Direction.Should().Be(direction);
        difference.Amount.Should().Be((decimal)amount);
        difference.Notes.Should().Be("açıklama");
        difference.RecordedBy.Should().Be(cashier);
        (await _ledger.ComputeExpectedCashAsync(sessionId)).Should().Be(100m);
    }

    [Fact]
    public async Task AFailingInTransactionStepRollsTheWholeCloseBack()
    {
        var (sessionId, cashier) = await CountedSessionAsync(100.00m, 90.00m);

        var act = () => CloseAsync(
            sessionId, cashier, 90.00m, explanation: "eksik",
            inTransaction: (_, _, _) => throw new InvalidOperationException("audit failed"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("audit failed");
        var record = (await _repository.GetByIdAsync(sessionId))!;
        record.Snapshot.Status.Should().Be(CashSessionStatus.Counting);
        record.DifferenceExplanation.Should().BeNull();
        (await _ledger.GetBySessionIdAsync(sessionId)).Should().NotContain(t => t.Type == CashTransactionType.ClosingDifference);
    }

    [Fact]
    public async Task TheCloseNeedsARecordedCountEqualToTheClosingAmount()
    {
        var cashier = Guid.NewGuid();
        var command = new OpenCashSessionCommand(Guid.NewGuid(), cashier, Guid.NewGuid(), 100.00m);
        await _service.OpenSessionWithOpeningEntryAsync(command);
        await _service.StartCountAsync(new StartCashCountCommand(command.CashSessionId, cashier));

        var none = () => CloseAsync(command.CashSessionId, cashier, 100m);
        await none.Should().ThrowAsync<CashCountRequiredException>();

        await _service.RecordCountAsync(new RecordCashCountCommand(command.CashSessionId, 90m, cashier));
        var mismatch = () => CloseAsync(command.CashSessionId, cashier, 100m, explanation: "x");
        await mismatch.Should().ThrowAsync<CashCountMismatchException>();
    }

    [Fact]
    public async Task OnlyTheCashierOrAnActorWhoMayActForThemStartsCountsRecordsAndCloses()
    {
        var cashier = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var command = new OpenCashSessionCommand(Guid.NewGuid(), cashier, Guid.NewGuid(), 100.00m);
        await _service.OpenSessionWithOpeningEntryAsync(command);

        var start = () => _service.StartCountAsync(new StartCashCountCommand(command.CashSessionId, stranger));
        await start.Should().ThrowAsync<CashSessionOwnerRequiredException>();
        await _service.StartCountAsync(new StartCashCountCommand(command.CashSessionId, stranger, MayActForOwner: true));

        var count = () => _service.RecordCountAsync(new RecordCashCountCommand(command.CashSessionId, 100m, stranger));
        await count.Should().ThrowAsync<CashSessionOwnerRequiredException>();
        await _service.RecordCountAsync(new RecordCashCountCommand(command.CashSessionId, 100m, stranger, MayActForOwner: true));

        var close = () => CloseAsync(command.CashSessionId, stranger, 100m);
        await close.Should().ThrowAsync<CashSessionOwnerRequiredException>();
        (await CloseAsync(command.CashSessionId, stranger, 100m, mayActForOwner: true)).Session.Status.Should().Be(CashSessionStatus.Closed);
    }

    [Fact]
    public async Task ReconcilingBySomeoneWhoRanOrClosedTheSessionNeedsTheAllowanceAndIsFlagged()
    {
        var (sessionId, cashier) = await CountedSessionAsync(100.00m, 100.00m);
        var closer = Guid.NewGuid();
        await CloseAsync(sessionId, closer, 100m, mayActForOwner: true);

        var byCloser = () => _service.ReconcileSessionAsync(new ReconcileCashSessionCommand(sessionId, closer));
        await byCloser.Should().ThrowAsync<CashReconcileSelfNotAllowedException>();
        var byCashier = () => _service.ReconcileSessionAsync(new ReconcileCashSessionCommand(sessionId, cashier));
        await byCashier.Should().ThrowAsync<CashReconcileSelfNotAllowedException>();
        (await _repository.GetByIdAsync(sessionId))!.Snapshot.Status.Should().Be(CashSessionStatus.Closed);

        var (reconciled, ev) = await _service.ReconcileSessionAsync(new ReconcileCashSessionCommand(sessionId, closer, "tek yönetici", SelfReconcileAllowed: true));

        reconciled.Status.Should().Be(CashSessionStatus.Reconciled);
        ev.SelfApproved.Should().BeTrue();
        (await _repository.GetByIdAsync(sessionId))!.ReconciledSelfApproved.Should().BeTrue();
    }

    [Fact]
    public async Task ATwoPartyReconcileIsNotFlaggedEvenWhenSelfReconciliationIsAllowed()
    {
        var (sessionId, cashier) = await CountedSessionAsync(100.00m, 100.00m);
        await CloseAsync(sessionId, cashier, 100m);

        var (_, ev) = await _service.ReconcileSessionAsync(
            new ReconcileCashSessionCommand(sessionId, Guid.NewGuid(), SelfReconcileAllowed: true));

        ev.SelfApproved.Should().BeFalse();
        (await _repository.GetByIdAsync(sessionId))!.ReconciledSelfApproved.Should().BeFalse();
    }

    [Fact]
    public async Task ParallelClosesCloseOnceAndPostTheDifferenceOnce()
    {
        var (sessionId, cashier) = await CountedSessionAsync(100.00m, 90.00m);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            try
            {
                await CloseAsync(sessionId, cashier, 90.00m, explanation: "eksik");
                return true;
            }
            catch (InvalidCashSessionStateException)
            {
                return false;
            }
        }));

        outcomes.Count(closed => closed).Should().Be(1);
        (await _ledger.GetBySessionIdAsync(sessionId)).Count(t => t.Type == CashTransactionType.ClosingDifference).Should().Be(1);
    }

    [Fact]
    public async Task ReconcileSessionAsyncRejectsANonClosedSession()
    {
        var command = new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100.00m);
        await _service.OpenSessionAsync(command);

        var act = () => _service.ReconcileSessionAsync(new ReconcileCashSessionCommand(command.CashSessionId, Guid.NewGuid()));

        await act.Should().ThrowAsync<InvalidCashSessionStateException>()
            .Where(ex => ex.CurrentStatus == CashSessionStatus.Open);
    }

    [Fact]
    public async Task LockedSessionOfAnUnknownSessionIsNotFound()
    {
        var act = () => _repository.InLockedSessionAsync(Guid.NewGuid(), _ => Task.FromResult(0));

        await act.Should().ThrowAsync<CashSessionNotFoundException>();
    }

    [Fact]
    public async Task SaveAsyncFailsClosedOnAStaleRowVersion()
    {
        var cashier = Guid.NewGuid();
        var command = new OpenCashSessionCommand(Guid.NewGuid(), cashier, Guid.NewGuid(), 100.00m);
        var (opened, _) = await _service.OpenSessionAsync(command);
        var record = (await _repository.GetByIdAsync(command.CashSessionId))!;

        // Someone else already saved this session (real row version is now
        // 2, via StartCount); replaying against the stale version 1 must
        // fail closed rather than silently overwrite the concurrent change.
        await _service.StartCountAsync(new StartCashCountCommand(command.CashSessionId, cashier));

        var act = () => _repository.SaveAsync(record, expectedRowVersion: opened.RowVersion);

        await act.Should().ThrowExactlyAsync<CashSessionConcurrencyException>();
    }

    [Fact]
    public async Task GetSuggestedOpeningBalanceAsyncReturnsNullWithoutAPriorClosedSession()
    {
        var terminalId = Guid.NewGuid();

        var suggestion = await _service.GetSuggestedOpeningBalanceAsync(terminalId);

        suggestion.Should().BeNull();
    }

    [Fact]
    public async Task GetSuggestedOpeningBalanceAsyncReturnsTheActualCashOfTheMostRecentClosedSession()
    {
        var terminalId = Guid.NewGuid();
        var (sessionId, cashier) = await CountedSessionAsync(100.00m, 137.50m, terminalId);
        await CloseAsync(sessionId, cashier, 137.50m, explanation: "fazla");

        var suggestion = await _service.GetSuggestedOpeningBalanceAsync(terminalId);

        suggestion.Should().Be(137.50m);
    }
}
