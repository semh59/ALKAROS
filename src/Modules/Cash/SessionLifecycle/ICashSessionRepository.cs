using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.TransactionLedger;
using Npgsql;

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

    /// <summary>
    /// Loads the terminal's active (Open, Counting or Closing) session: at most one, as the unique index
    /// guarantees. Used by the single-open-session policy check and the active-session endpoint, so neither
    /// reads the terminal's whole history.
    /// </summary>
    Task<IReadOnlyList<CashSessionSnapshot>> GetActiveByTerminalIdAsync(Guid terminalId, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Runs <paramref name="work"/> in one transaction that holds the session row exclusively (<c>FOR UPDATE</c>):
    /// counting, closing and reconciling wait for each other, and whatever <paramref name="work"/> reads (the latest
    /// count, the expected cash) cannot change before it writes. The transaction commits when the work returns and
    /// rolls back when it throws. Throws <see cref="CashSessionNotFoundException"/> for an unknown session.
    /// </summary>
    Task<T> InLockedSessionAsync<T>(
        Guid cashSessionId, Func<ILockedCashSession, Task<T>> work, CancellationToken cancellationToken = default);

    /// <summary>Loads every count recorded against a session, oldest first.</summary>
    Task<IReadOnlyList<CashCountEntry>> GetCountsAsync(Guid cashSessionId, CancellationToken cancellationToken = default);
}

/// <summary>One cash session as seen from inside <see cref="ICashSessionRepository.InLockedSessionAsync{T}"/>.</summary>
public interface ILockedCashSession
{
    /// <summary>The session as it stood when the lock was taken.</summary>
    CashSessionRecord Record { get; }

    /// <summary>The transaction's connection and transaction, for writes of the caller that must commit with the session.</summary>
    NpgsqlConnection Connection { get; }

    NpgsqlTransaction Transaction { get; }

    /// <summary>The expected cash reconstructed from the session's ledger inside the lock.</summary>
    Task<decimal> ReadExpectedCashAsync(CancellationToken cancellationToken = default);

    /// <summary>The newest recorded physical count, or null when none was recorded.</summary>
    Task<decimal?> ReadLatestCountAsync(CancellationToken cancellationToken = default);

    /// <summary>The distinct users who recorded a count for the session.</summary>
    Task<IReadOnlyList<Guid>> ReadCountersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a physical cash count entry against the session (does not change the session's own row). A retried
    /// request (same counter, amount and notes within a short window) finds the row the first attempt wrote.
    /// </summary>
    Task<Guid> RecordCountAsync(decimal countedAmount, Guid countedBy, string? notes, CancellationToken cancellationToken = default);

    /// <summary>Persists the changed session row; returns the incremented row version.</summary>
    Task<long> SaveAsync(CashSessionRecord updated, CancellationToken cancellationToken = default);

    /// <summary>Appends a ledger entry that commits or rolls back with the session change.</summary>
    Task PostLedgerEntryAsync(CashTransaction entry, CancellationToken cancellationToken = default);
}
