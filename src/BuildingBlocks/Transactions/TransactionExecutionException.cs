namespace ALKAROS.Transactions;

/// <summary>
/// Thrown when a transaction could not be completed or rolled back safely,
/// for example when the rollback of an enlisted resource itself fails. The
/// original failure is preserved in <see cref="Exception.InnerException"/>.
/// </summary>
public sealed class TransactionExecutionException : Exception
{
    /// <summary>
    /// Creates a transaction execution failure with the original failure as
    /// the inner exception.
    /// </summary>
    public TransactionExecutionException(string message, Exception innerException)
        : base(message, innerException)
    {
        ArgumentNullException.ThrowIfNull(innerException);
    }
}
