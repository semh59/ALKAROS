using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.SessionLifecycle.Tests.Fixtures;
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
    private readonly CashSessionLifecycleService _service;

    public CashSessionLifecycleServiceTests(CashSessionTestDatabase database)
    {
        _repository = new PostgresCashSessionRepository(database.DataSource);
        _service = new CashSessionLifecycleService(_repository, new CashSessionPolicy());
    }

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
        var first = new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), terminalId, 100.00m);
        await _service.OpenSessionAsync(first);
        await _service.CloseSessionAsync(
            new CloseCashSessionCommand(first.CashSessionId, 100.00m, Guid.NewGuid()), expectedCash: 100.00m);

        var second = new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), terminalId, 150.00m);
        var (session, _) = await _service.OpenSessionAsync(second);

        session.Status.Should().Be(CashSessionStatus.Open);
    }

    [Fact]
    public async Task ConcurrentOpenSessionAsyncCallsOnTheSameTerminalNeverBothSucceed()
    {
        // The GetByTerminalIdAsync check and the insert are not atomic; two
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
        var command = new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100.00m);
        await _service.OpenSessionAsync(command);
        await _service.CloseSessionAsync(
            new CloseCashSessionCommand(command.CashSessionId, 100.00m, Guid.NewGuid()), expectedCash: 100.00m);

        var act = () => _service.CloseSessionAsync(
            new CloseCashSessionCommand(command.CashSessionId, 100.00m, Guid.NewGuid()), expectedCash: 100.00m);

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
        var command = new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100.00m);
        await _service.OpenSessionAsync(command);
        await _service.CloseSessionAsync(
            new CloseCashSessionCommand(command.CashSessionId, 100.00m, Guid.NewGuid()), expectedCash: 100.00m);

        var act = () => _service.StartCountAsync(new StartCashCountCommand(command.CashSessionId, Guid.NewGuid()));

        await act.Should().ThrowAsync<InvalidCashSessionStateException>()
            .Where(ex => ex.CurrentStatus == CashSessionStatus.Closed);
    }

    [Fact]
    public async Task FullLifecycleOpenCountCloseReconcileReachesReconciledWithCorrectDifference()
    {
        var command = new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 200.00m);
        await _service.OpenSessionAsync(command);

        var (counting, _) = await _service.StartCountAsync(new StartCashCountCommand(command.CashSessionId, Guid.NewGuid()));
        counting.Status.Should().Be(CashSessionStatus.Counting);

        var countEvent = await _service.RecordCountAsync(
            new RecordCashCountCommand(command.CashSessionId, 195.00m, Guid.NewGuid(), "Sayim 1"));
        countEvent.CountedAmount.Should().Be(195.00m);

        var closedBy = Guid.NewGuid();
        var (closed, closedEvent) = await _service.CloseSessionAsync(
            new CloseCashSessionCommand(command.CashSessionId, 195.00m, closedBy), expectedCash: 200.00m);

        closed.Status.Should().Be(CashSessionStatus.Closed);
        closed.Difference.Should().Be(-5.00m);
        closedEvent.Difference.Should().Be(-5.00m);
        closedEvent.ClosedBy.Should().Be(closedBy);

        var reconciledBy = Guid.NewGuid();
        var (reconciled, reconciledEvent) = await _service.ReconcileSessionAsync(
            new ReconcileCashSessionCommand(command.CashSessionId, reconciledBy, "Gunluk mutabakat"));

        reconciled.Status.Should().Be(CashSessionStatus.Reconciled);
        reconciledEvent.ReconciledBy.Should().Be(reconciledBy);

        var counts = await _repository.GetCountsAsync(command.CashSessionId);
        counts.Should().ContainSingle(c => c.CountedAmount == 195.00m && c.Notes == "Sayim 1");
    }

    [Fact]
    public async Task CloseSessionAsyncBeyondVarianceToleranceRequiresSupervisorOverride()
    {
        var command = new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100.00m);
        await _service.OpenSessionAsync(command);

        // 200 vs expected 100 => difference 100, over the default 50 tolerance.
        var withoutOverride = () => _service.CloseSessionAsync(
            new CloseCashSessionCommand(command.CashSessionId, 200.00m, Guid.NewGuid()), expectedCash: 100.00m);
        await withoutOverride.Should().ThrowAsync<CashVarianceThresholdExceededException>();

        var (closed, _) = await _service.CloseSessionAsync(
            new CloseCashSessionCommand(
                command.CashSessionId, 200.00m, Guid.NewGuid(),
                IsSupervisorOverride: true, OverrideReason: "Sayim farki onaylandi"),
            expectedCash: 100.00m);

        closed.Status.Should().Be(CashSessionStatus.Closed);
        closed.Difference.Should().Be(100.00m);
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
    public async Task SaveAsyncFailsClosedOnAStaleRowVersion()
    {
        var command = new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100.00m);
        var (opened, _) = await _service.OpenSessionAsync(command);
        var record = (await _repository.GetByIdAsync(command.CashSessionId))!;

        // Someone else already saved this session (real row version is now
        // 2, via StartCount); replaying against the stale version 1 must
        // fail closed rather than silently overwrite the concurrent change.
        await _service.StartCountAsync(new StartCashCountCommand(command.CashSessionId, Guid.NewGuid()));

        var act = () => _repository.SaveAsync(record, expectedRowVersion: opened.RowVersion);

        await act.Should().ThrowAsync<InvalidOperationException>();
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
        var command = new OpenCashSessionCommand(Guid.NewGuid(), Guid.NewGuid(), terminalId, 100.00m);
        await _service.OpenSessionAsync(command);
        await _service.CloseSessionAsync(
            new CloseCashSessionCommand(command.CashSessionId, 137.50m, Guid.NewGuid()), expectedCash: 100.00m);

        var suggestion = await _service.GetSuggestedOpeningBalanceAsync(terminalId);

        suggestion.Should().Be(137.50m);
    }
}
