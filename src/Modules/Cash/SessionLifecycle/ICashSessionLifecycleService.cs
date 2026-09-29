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
    /// Closes the session. <paramref name="expectedCash"/> is supplied by
    /// the caller rather than computed here — V13-CSH-001 has no ledger yet
    /// (V13-CSH-002's scope), so today it is simply the session's own
    /// opening balance; once the ledger lands, the caller sums it instead.
    /// </summary>
    Task<(CashSessionSnapshot Session, CashSessionClosedEvent Event)> CloseSessionAsync(
        CloseCashSessionCommand command, decimal expectedCash, CancellationToken cancellationToken = default);

    Task<(CashSessionSnapshot Session, CashSessionReconciledEvent Event)> ReconcileSessionAsync(
        ReconcileCashSessionCommand command, CancellationToken cancellationToken = default);

    /// <summary>Read-only, informational suggestion for a terminal's next opening balance (see repository doc).</summary>
    Task<decimal?> GetSuggestedOpeningBalanceAsync(Guid terminalId, CancellationToken cancellationToken = default);
}
