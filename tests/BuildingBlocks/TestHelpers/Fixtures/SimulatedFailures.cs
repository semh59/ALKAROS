using ALKAROS.Transactions;

namespace ALKAROS.TestHelpers;

/// <summary>
/// A permanent or unknown failure used in tests. Never classified as
/// transient by <see cref="DefaultRetryClassifier"/>.
/// </summary>
public sealed class SimulatedFailureException : Exception
{
    public SimulatedFailureException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// A transient failure that is explicitly marked retryable through
/// <see cref="ITransientFailure"/>.
/// </summary>
public sealed class SimulatedTransientException : Exception, ITransientFailure
{
    public SimulatedTransientException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// A classifier that always returns a fixed classification.
/// </summary>
public sealed class FixedClassifier : IRetryClassifier
{
    private readonly RetryClassification _classification;

    public FixedClassifier(RetryClassification classification)
    {
        _classification = classification;
    }

    public RetryClassification Classify(Exception exception) => _classification;
}