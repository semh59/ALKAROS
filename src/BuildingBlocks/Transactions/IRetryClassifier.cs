namespace ALKAROS.Transactions;

/// <summary>
/// Classifies exceptions for retry decisions.
/// </summary>
public interface IRetryClassifier
{
    /// <summary>
    /// Classifies <paramref name="exception"/> as transient or non-transient.
    /// </summary>
    RetryClassification Classify(Exception exception);
}
