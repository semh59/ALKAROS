using ALKAROS.Messaging;
using ALKAROS.Transactions;

namespace ALKAROS.TransactionOutboxIntegration.Tests.Fixtures;

/// <summary>
/// A permanent or unknown failure used in tests. Never classified as
/// transient by <see cref="DefaultRetryClassifier"/>.
/// </summary>
internal sealed class SimulatedFailureException : Exception
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
internal sealed class SimulatedTransientException : Exception, ITransientFailure
{
    public SimulatedTransientException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// A classifier that always returns a fixed classification.
/// </summary>
internal sealed class FixedClassifier : IRetryClassifier
{
    private readonly RetryClassification _classification;

    public FixedClassifier(RetryClassification classification)
    {
        _classification = classification;
    }

    public RetryClassification Classify(Exception exception) => _classification;
}

/// <summary>
/// A delivery sink that records every delivered message.
/// </summary>
internal sealed class RecordingSink : IOutboxDeliverySink
{
    private readonly List<OutboxMessage> _delivered = new();

    public IReadOnlyList<OutboxMessage> Delivered => _delivered;

    public Task<bool> HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        _delivered.Add(message);
        return Task.FromResult(true);
    }
}

/// <summary>
/// A domain consumer that is idempotent per message id: only the first
/// delivery of a message produces a business effect, later duplicate
/// deliveries are acknowledged without one (V0-ARC-003 §3).
/// </summary>
internal sealed class IdempotentSink : IOutboxDeliverySink
{
    private readonly HashSet<Guid> _handled = new();

    public int BusinessEffectCount { get; private set; }

    public Task<bool> HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (_handled.Add(message.Id))
            BusinessEffectCount++;
        return Task.FromResult(true);
    }
}
