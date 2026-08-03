using System.Data.Common;

namespace ALKAROS.Transactions;

/// <summary>
/// A unit of work that writes to the database transaction owned by
/// <see cref="TransactionContext.RunAsync"/>. Modules implement this contract
/// for their repositories and register through <see cref="ITransactionContext.Enlist"/>.
/// A resource commits through the database session, so its writes are atomic
/// with the transaction: everything commits together or nothing commits.
/// External side effects (HTTP calls, files, third-party APIs) cannot be
/// atomic with a database transaction and are forbidden in this contract;
/// they must be written as outbox messages inside the transaction and
/// processed after the commit.
/// </summary>
public interface ITransactionResource
{
    /// <summary>
    /// Persists the resource's writes using the database session owned by
    /// the current transaction scope. Called once per successful workflow,
    /// in enlistment order, before the database transaction commits.
    /// A resource that does not implement this overload cannot participate
    /// in a database-backed transaction: the default behavior fails the
    /// commit so an external side effect is never applied before the
    /// database commit.
    /// </summary>
    Task CommitAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
        => throw new InvalidOperationException(
            "This resource cannot commit through the database session; "
            + "external side effects must be written as outbox messages and "
            + "processed after the transaction commits.");

    /// <summary>
    /// Persists the resource's writes without a database session. Only
    /// available to transaction scopes that own no database connection;
    /// database-backed scopes always call the session overload, whose
    /// default behavior fails the commit so an external side effect is
    /// never applied before the database commit.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Undoes the resource's writes when the transaction fails.
    /// Called in reverse enlistment order.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken);
}
