using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.TransactionLedger;
using Npgsql;

namespace ALKAROS.Cash.SessionLifecycle;

public sealed class CashSessionLifecycleService : ICashSessionLifecycleService
{
    private readonly ICashSessionRepository _repository;
    private readonly ICashSessionPolicy _policy;

    public CashSessionLifecycleService(ICashSessionRepository repository, ICashSessionPolicy policy)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public Task<(CashSessionSnapshot Session, CashSessionOpenedEvent Event)> OpenSessionAsync(
        OpenCashSessionCommand command, CancellationToken cancellationToken = default)
        => OpenAsync(command, postOpeningEntry: false, cancellationToken);

    public Task<(CashSessionSnapshot Session, CashSessionOpenedEvent Event)> OpenSessionWithOpeningEntryAsync(
        OpenCashSessionCommand command, CancellationToken cancellationToken = default)
        => OpenAsync(command, postOpeningEntry: true, cancellationToken);

    private async Task<(CashSessionSnapshot Session, CashSessionOpenedEvent Event)> OpenAsync(
        OpenCashSessionCommand command, bool postOpeningEntry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Validate();

        var existingTerminalSessions = await _repository.GetByTerminalIdAsync(command.TerminalId, cancellationToken);
        _policy.ValidateCanOpenSession(command, existingTerminalSessions);

        var now = DateTimeOffset.UtcNow;
        var snapshot = new CashSessionSnapshot(
            command.CashSessionId,
            command.CashierUserId,
            command.TerminalId,
            CashSessionStatus.Open,
            command.OpeningBalance,
            ExpectedCash: command.OpeningBalance,
            ActualCash: 0m,
            Difference: 0m,
            OpenedAt: now,
            ClosedAt: null,
            RowVersion: 1);
        var record = new CashSessionRecord(snapshot, ClosedBy: null, IsSupervisorOverride: false,
            OverrideReason: null, ReconciledBy: null, ReconciliationNotes: null, ReconciledAt: null);

        try
        {
            if (postOpeningEntry && command.OpeningBalance > 0)
            {
                await _repository.AddAsync(
                    record,
                    new CashTransaction(
                        Guid.NewGuid(), command.CashSessionId, CashTransactionType.Opening,
                        command.OpeningBalance, CashTransactionDirection.In,
                        recordedBy: command.CashierUserId, occurredAt: now),
                    cancellationToken);
            }
            else
            {
                await _repository.AddAsync(record, cancellationToken);
            }
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation
            && ex.ConstraintName == "ux_cash_sessions_one_active_per_terminal")
        {
            // The GetByTerminalIdAsync check above and this insert are not
            // atomic — two concurrent OpenSessionAsync calls for the same
            // terminal can both pass the in-memory policy check and race
            // to insert. CSH-INV-01's real enforcement is the partial
            // unique index (defense in depth, migration 122); this only
            // translates that race's raw constraint violation back into
            // the same domain exception a sequential caller would see.
            var stillActive = await _repository.GetByTerminalIdAsync(command.TerminalId, cancellationToken);
            var winner = stillActive.First(s => s.IsActive);
            throw new ActiveCashSessionExistsException(command.TerminalId, winner.CashSessionId);
        }

        return (snapshot, new CashSessionOpenedEvent(
            command.CashSessionId, command.CashierUserId, command.TerminalId, command.OpeningBalance, now));
    }

    public async Task<(CashSessionSnapshot Session, CashCountStartedEvent Event)> StartCountAsync(
        StartCashCountCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Validate();

        var record = await _repository.GetByIdAsync(command.CashSessionId, cancellationToken)
            ?? throw new CashSessionNotFoundException(command.CashSessionId);
        _policy.ValidateCanStartCount(record.Snapshot, command.CashierUserId);

        var now = DateTimeOffset.UtcNow;
        var updatedSnapshot = record.Snapshot with { Status = CashSessionStatus.Counting };
        var newRowVersion = await _repository.SaveAsync(
            record with { Snapshot = updatedSnapshot }, record.Snapshot.RowVersion, cancellationToken);

        return (updatedSnapshot with { RowVersion = newRowVersion },
            new CashCountStartedEvent(command.CashSessionId, command.CashierUserId, now));
    }

    public async Task<CashCountRecordedEvent> RecordCountAsync(
        RecordCashCountCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Validate();

        var record = await _repository.GetByIdAsync(command.CashSessionId, cancellationToken)
            ?? throw new CashSessionNotFoundException(command.CashSessionId);
        _policy.ValidateCanRecordCount(record.Snapshot, command.CountedAmount);

        await _repository.RecordCountAsync(
            command.CashSessionId, command.CountedAmount, command.CountedBy, command.Notes, cancellationToken);

        return new CashCountRecordedEvent(
            command.CashSessionId, command.CountedAmount, command.CountedBy, command.Notes, DateTimeOffset.UtcNow);
    }

    public async Task<(CashSessionSnapshot Session, CashSessionClosedEvent Event)> CloseSessionAsync(
        CloseCashSessionCommand command, decimal expectedCash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Validate();

        var record = await _repository.GetByIdAsync(command.CashSessionId, cancellationToken)
            ?? throw new CashSessionNotFoundException(command.CashSessionId);

        var difference = _policy.ValidateCanCloseSession(
            record.Snapshot, command.ActualCash, expectedCash, command.IsSupervisorOverride);

        var now = DateTimeOffset.UtcNow;
        var updatedSnapshot = record.Snapshot with
        {
            Status = CashSessionStatus.Closed,
            ExpectedCash = expectedCash,
            ActualCash = command.ActualCash,
            Difference = difference,
            ClosedAt = now,
        };
        var updatedRecord = record with
        {
            Snapshot = updatedSnapshot,
            ClosedBy = command.ClosedBy,
            IsSupervisorOverride = command.IsSupervisorOverride,
            OverrideReason = command.OverrideReason,
        };
        var newRowVersion = await _repository.SaveAsync(updatedRecord, record.Snapshot.RowVersion, cancellationToken);
        updatedSnapshot = updatedSnapshot with { RowVersion = newRowVersion };

        return (updatedSnapshot, new CashSessionClosedEvent(
            command.CashSessionId, expectedCash, command.ActualCash, difference,
            command.ClosedBy, command.IsSupervisorOverride, command.OverrideReason, now));
    }

    public async Task<(CashSessionSnapshot Session, CashSessionReconciledEvent Event)> ReconcileSessionAsync(
        ReconcileCashSessionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Validate();

        var record = await _repository.GetByIdAsync(command.CashSessionId, cancellationToken)
            ?? throw new CashSessionNotFoundException(command.CashSessionId);
        _policy.ValidateCanReconcile(record.Snapshot);

        var now = DateTimeOffset.UtcNow;
        var updatedSnapshot = record.Snapshot with { Status = CashSessionStatus.Reconciled };
        var updatedRecord = record with
        {
            Snapshot = updatedSnapshot,
            ReconciledBy = command.ReconciledBy,
            ReconciliationNotes = command.Notes,
            ReconciledAt = now,
        };
        var newRowVersion = await _repository.SaveAsync(updatedRecord, record.Snapshot.RowVersion, cancellationToken);
        updatedSnapshot = updatedSnapshot with { RowVersion = newRowVersion };

        return (updatedSnapshot, new CashSessionReconciledEvent(
            command.CashSessionId, command.ReconciledBy, command.Notes, now));
    }

    public Task<decimal?> GetSuggestedOpeningBalanceAsync(Guid terminalId, CancellationToken cancellationToken = default)
        => _repository.GetSuggestedOpeningBalanceAsync(terminalId, cancellationToken);
}
