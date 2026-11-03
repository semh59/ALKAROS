using ALKAROS.Cash.Contracts;

namespace ALKAROS.Cash.SessionLifecycle;

/// <summary>
/// Orchestrates the CashSession lifecycle commands (V1-CSH-001) against
/// persistence (V13-CSH-001): loads current state, delegates every
/// invariant decision to <see cref="ICashSessionPolicy"/>, persists the
/// result, and returns the resulting snapshot plus the domain event a
/// caller may publish.
/// </summary>
public interface ICashSessionLifecycleService
{
    Task<(CashSessionSnapshot Session, CashSessionOpenedEvent Event)> OpenSessionAsync(
        OpenCashSessionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// V1-RMD-416 (V1-RMD-393 F-14): same as <see cref="OpenSessionAsync"/>, and when the opening balance is
    /// positive also posts the session's <c>Opening</c> ledger entry in the same transaction. The caller used to
    /// post it as a second, separate write; when that write failed the session stayed open with no float in its
    /// ledger, and its expected cash started at 0.
    /// </summary>
    Task<(CashSessionSnapshot Session, CashSessionOpenedEvent Event)> OpenSessionWithOpeningEntryAsync(
        OpenCashSessionCommand command, CancellationToken cancellationToken = default);

    Task<(CashSessionSnapshot Session, CashCountStartedEvent Event)> StartCountAsync(
        StartCashCountCommand command, CancellationToken cancellationToken = default);

    Task<CashCountRecordedEvent> RecordCountAsync(
        RecordCashCountCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the session in one transaction that holds the session row: the expected cash and the latest count are
    /// read under that lock, the session is updated, and a non-zero difference is posted to the ledger.
    /// <paramref name="varianceTolerance"/> is the manager-set tolerance. <paramref name="inTransaction"/> runs last
    /// in the same transaction (the caller's audit event commits or rolls back with the close).
    /// </summary>
    Task<(CashSessionSnapshot Session, CashSessionClosedEvent Event)> CloseSessionAsync(
        CloseCashSessionCommand command,
        decimal varianceTolerance,
        Func<CashSessionClosedEvent, ILockedCashSession, CancellationToken, Task>? inTransaction = null,
        CancellationToken cancellationToken = default);

    /// <summary>Reconciles a closed session; <paramref name="inTransaction"/> runs in the same transaction.</summary>
    Task<(CashSessionSnapshot Session, CashSessionReconciledEvent Event)> ReconcileSessionAsync(
        ReconcileCashSessionCommand command,
        Func<CashSessionReconciledEvent, ILockedCashSession, CancellationToken, Task>? inTransaction = null,
        CancellationToken cancellationToken = default);

    /// <summary>Read-only, informational suggestion for a terminal's next opening balance (see repository doc).</summary>
    Task<decimal?> GetSuggestedOpeningBalanceAsync(Guid terminalId, CancellationToken cancellationToken = default);
}
