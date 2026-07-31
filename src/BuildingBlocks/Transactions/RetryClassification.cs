namespace ALKAROS.Transactions;

/// <summary>
/// Outcome of a retry classifier for a single exception.
/// </summary>
public enum RetryClassification
{
    /// <summary>
    /// The failure is transient and retrying the operation is permitted.
    /// </summary>
    Transient,

    /// <summary>
    /// The failure is permanent or unknown; the operation must not be retried.
    /// </summary>
    NonTransient,
}
