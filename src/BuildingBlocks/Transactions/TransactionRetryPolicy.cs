namespace ALKAROS.Transactions;

/// <summary>
/// Defines whether and how <see cref="TransactionContext.RunAsync"/> retries
/// a failed workflow. The default policy never retries: unknown and
/// non-transient failures must surface immediately.
/// </summary>
public sealed class TransactionRetryPolicy
{
    /// <summary>
    /// Creates a retry policy with the given attempt count, delay and
    /// classifier.
    /// </summary>
    /// <param name="maxAttempts">
    /// Total number of execution attempts, including the first one.
    /// A value of 1 disables retries.
    /// </param>
    /// <param name="delayForAttempt">
    /// Delay before each retry; receives the number of already-completed
    /// attempts (1 for the first retry). Defaults to no delay.
    /// </param>
    /// <param name="classifier">
    /// Classifier used to decide whether a failure may be retried.
    /// Defaults to <see cref="DefaultRetryClassifier"/>.
    /// </param>
    public TransactionRetryPolicy(
        int maxAttempts = 1,
        Func<int, TimeSpan>? delayForAttempt = null,
        IRetryClassifier? classifier = null)
    {
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(
                nameof(maxAttempts),
                "MaxAttempts must be at least 1.");

        MaxAttempts = maxAttempts;
        DelayForAttempt = delayForAttempt ?? (_ => TimeSpan.Zero);
        Classifier = classifier ?? DefaultRetryClassifier.Instance;
    }

    /// <summary>
    /// Total number of execution attempts, including the first one.
    /// </summary>
    public int MaxAttempts { get; }

    /// <summary>
    /// Delay before each retry. Receives the number of already-completed
    /// attempts (1 for the first retry).
    /// </summary>
    public Func<int, TimeSpan> DelayForAttempt { get; }

    /// <summary>
    /// Classifier used to decide whether a failure may be retried.
    /// </summary>
    public IRetryClassifier Classifier { get; }

    /// <summary>
    /// Returns true when the failure of attempt
    /// <paramref name="completedAttempts"/> may be retried: more attempts
    /// remain and the exception is classified as transient.
    /// </summary>
    public bool MayRetry(int completedAttempts, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return completedAttempts < MaxAttempts
            && Classifier.Classify(exception) == RetryClassification.Transient;
    }
}
