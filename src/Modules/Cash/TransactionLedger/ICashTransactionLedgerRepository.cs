using Npgsql;

namespace ALKAROS.Cash.TransactionLedger;

/// <summary>Persistence contract for the append-only CashTransaction ledger (V13-CSH-002).</summary>
public interface ICashTransactionLedgerRepository
{
    /// <summary>Persists a new, already-validated ledger entry. Entries are never updated.</summary>
    Task RecordAsync(CashTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Same as <see cref="RecordAsync(CashTransaction, CancellationToken)"/> inside the caller's transaction (V13-CSH-003 composes with this).</summary>
    Task RecordAsync(
        CashTransaction transaction,
        NpgsqlConnection connection,
        NpgsqlTransaction dbTransaction,
        CancellationToken cancellationToken = default);

    /// <summary>Loads every entry recorded against a session, oldest first.</summary>
    Task<IReadOnlyList<CashTransaction>> GetBySessionIdAsync(Guid cashSessionId, CancellationToken cancellationToken = default);

    /// <summary>V1-RMD-241: the entry (if any) already recorded under this session+key, for idempotent replay.</summary>
    Task<CashTransaction?> GetBySessionAndIdempotencyKeyAsync(
        Guid cashSessionId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>The same lookup inside the caller's transaction (no second pooled connection while a lock is held).</summary>
    Task<CashTransaction?> GetBySessionAndIdempotencyKeyAsync(
        Guid cashSessionId, string idempotencyKey, NpgsqlConnection connection, NpgsqlTransaction dbTransaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The session's expected cash reconstructed purely from its own
    /// immutable ledger entries (Acceptance evidence) — sums every entry
    /// that <see cref="CashTransaction.ContributesToExpectedCash"/>, signed
    /// by direction. Never silently overwritten: a session with no
    /// Opening entry yet simply sums to zero.
    /// </summary>
    Task<decimal> ComputeExpectedCashAsync(Guid cashSessionId, CancellationToken cancellationToken = default);

    /// <summary>The same sum inside the caller's transaction (a close reads it while it holds the session row).</summary>
    Task<decimal> ComputeExpectedCashAsync(
        Guid cashSessionId, NpgsqlConnection connection, NpgsqlTransaction dbTransaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// locks the session row exclusively (so cash-outs and approvals on one drawer run one at a time),
    /// requires it to be Open and returns the expected cash read under that lock - the number a cash-out is
    /// checked against in the same transaction that then records it.
    /// </summary>
    Task<decimal> LockOpenSessionAndReadExpectedCashAsync(
        Guid cashSessionId, NpgsqlConnection connection, NpgsqlTransaction dbTransaction, CancellationToken cancellationToken = default);
}
