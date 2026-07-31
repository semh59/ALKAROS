namespace ALKAROS.Transactions;

/// <summary>
/// Classifies an exception as <see cref="RetryClassification.Transient"/>
/// only when the exception itself or one of its inner exceptions implements
/// <see cref="ITransientFailure"/>. Every other exception is classified as
/// <see cref="RetryClassification.NonTransient"/> so unknown failures are
/// never automatically retried.
/// </summary>
public sealed class DefaultRetryClassifier : IRetryClassifier
{
    /// <summary>
    /// The shared classifier instance. Stateless and thread-safe.
    /// </summary>
    public static readonly DefaultRetryClassifier Instance = new();

    private DefaultRetryClassifier()
    {
    }

    /// <inheritdoc />
    public RetryClassification Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is ITransientFailure)
                return RetryClassification.Transient;
        }

        return RetryClassification.NonTransient;
    }
}
