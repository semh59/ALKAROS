namespace ALKAROS.Transactions;

/// <summary>
/// Thrown when an independent transaction is requested while an ambient
/// transaction is already active. Nested calls must join the ambient
/// transaction instead.
/// </summary>
public sealed class NestedTransactionException : InvalidOperationException
{
    /// <summary>
    /// Creates a nested transaction rejection.
    /// </summary>
    public NestedTransactionException()
        : base("An independent transaction cannot be started inside an active transaction scope.")
    {
    }
}
