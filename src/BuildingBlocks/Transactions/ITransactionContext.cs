using System.Data.Common;

namespace ALKAROS.Transactions;

/// <summary>
/// The ambient transaction context handed to a workflow. Every resource
/// enlisted here commits together or rolls back completely.
/// </summary>
public interface ITransactionContext
{
    /// <summary>
    /// The unique identifier of the current transaction. Nested calls that
    /// join the ambient transaction share the same identifier.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// The shared database connection for this transaction. It is available
    /// only when the transaction was started with a database data source.
    /// </summary>
    DbConnection Connection { get; }

    /// <summary>
    /// The shared database transaction for this transaction. Persistent
    /// writes must assign it to their commands.
    /// </summary>
    DbTransaction Transaction { get; }

    /// <summary>
    /// Registers <paramref name="resource"/> in the current transaction.
    /// </summary>
    void Enlist(ITransactionResource resource);
}
