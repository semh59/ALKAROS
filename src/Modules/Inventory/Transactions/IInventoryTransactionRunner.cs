using Npgsql;

namespace ALKAROS.Inventory.Transactions;

/// <summary>
/// V1-RMD-125: lets a service orchestrate several Inventory repositories'
/// writes (ledger append, waste record insert, guarded balance apply) as
/// one atomic transaction without depending on NpgsqlDataSource directly —
/// keeping the service constructible with fakes for pure business-rule
/// tests, while the real (Postgres) implementation still commits
/// everything as a single database transaction.
/// </summary>
public interface IInventoryTransactionRunner
{
    /// <summary>
    /// Runs <paramref name="operation"/> inside one connection/transaction,
    /// committing when it returns and rolling back (Npgsql's own
    /// dispose-driven rollback on an uncommitted transaction) when it
    /// throws.
    /// </summary>
    Task<T> RunAsync<T>(Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> operation, CancellationToken ct = default);
}
