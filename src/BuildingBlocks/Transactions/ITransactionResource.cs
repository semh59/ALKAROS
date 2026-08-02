using System.Data.Common;

namespace ALKAROS.Transactions;

/// <summary>
/// A unit of work that participates in the ambient transaction started by
/// <see cref="TransactionContext.RunAsync"/>. Modules implement this contract
/// for their repositories (for example over a shared database transaction)
/// and register through <see cref="ITransactionContext.Enlist"/>.
/// </summary>
public interface ITransactionResource
{
    /// <summary>
    /// Persists the resource's writes as part of the shared transaction.
    /// Called once per successful workflow, in enlistment order.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Persists the resource using the database session owned by the current
    /// transaction scope. Resources that do not write to a database retain
    /// the original commit behavior.
    /// </summary>
    Task CommitAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
        => CommitAsync(cancellationToken);

    /// <summary>
    /// Undoes the resource's writes when the transaction fails.
    /// Called in reverse enlistment order.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken);
}
