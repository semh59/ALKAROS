using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.TransactionLedger;

namespace ALKAROS.Cash.SessionLifecycle;

/// <summary>
/// Persistence contract for the CashSession lifecycle (V13-CSH-001).
/// Concurrency is guarded by the optimistic row version on the session row
/// (same pattern as IPaymentRepository/IBillRepository).
/// </summary>
public interface ICashSessionRepository
{
    /// <summary>Loads a cash session (with its audit-trail fields) by id. Returns null if not found.</summary>
    Task<CashSessionRecord?> GetByIdAsync(Guid cashSessionId, CancellationToken cancellationToken = default);

    /// <summary>Loads every session ever opened on a terminal (used by the single-open-session policy check).</summary>
    Task<IReadOnlyList<CashSessionSnapshot>> GetByTerminalIdAsync(Guid terminalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The real (counted) actual cash of the terminal's most recent
    /// Closed/Reconciled session, or null if the terminal has never had one.
    /// Read-only and informational (V13-CSH-001 In scope, PO:2026-09-16):
    /// OpenSession itself never treats this as authoritative — only the UI
    /// (V13-PUI-002) pre-fills an opening balance suggestion with it.
    /// </summary>
    Task<decimal?> GetSuggestedOpeningBalanceAsync(Guid terminalId, CancellationToken cancellationToken = default);

    /// <summary>Inserts a newly opened session.</summary>
    Task AddAsync(CashSessionRecord session, CancellationToken cancellationToken = default);

    /// <summary>
    /// V1-RMD-416: inserts a newly opened session and its <c>Opening</c> ledger entry in one transaction, so a
    /// session never exists without the float it was opened with.
    /// </summary>
    Task AddAsync(CashSessionRecord session, CashTransaction openingEntry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a state change on an existing session. Fails if the current
    /// row version differs from <paramref name="expectedRowVersion"/>.
    /// Returns the incremented row version.
    /// </summary>
    Task<long> SaveAsync(CashSessionRecord session, long expectedRowVersion, CancellationToken cancellationToken = default);

    /// <summary>Appends a physical cash count entry against a session (does not change the session's own row).</summary>
    Task<Guid> RecordCountAsync(
        Guid cashSessionId,
        decimal countedAmount,
        Guid countedBy,
        string? notes,
        CancellationToken cancellationToken = default);

    /// <summary>Loads every count recorded against a session, oldest first.</summary>
    Task<IReadOnlyList<CashCountEntry>> GetCountsAsync(Guid cashSessionId, CancellationToken cancellationToken = default);
}
