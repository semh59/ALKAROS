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
    /// Registers <paramref name="resource"/> in the current transaction.
    /// </summary>
    void Enlist(ITransactionResource resource);
}
