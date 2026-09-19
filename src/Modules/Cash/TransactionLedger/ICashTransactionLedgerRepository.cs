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

    /// <summary>
    /// The session's expected cash reconstructed purely from its own
    /// immutable ledger entries (Acceptance evidence) — sums every entry
    /// that <see cref="CashTransaction.ContributesToExpectedCash"/>, signed
    /// by direction. Never silently overwritten: a session with no
    /// Opening entry yet simply sums to zero.
    /// </summary>
    Task<decimal> ComputeExpectedCashAsync(Guid cashSessionId, CancellationToken cancellationToken = default);
}
