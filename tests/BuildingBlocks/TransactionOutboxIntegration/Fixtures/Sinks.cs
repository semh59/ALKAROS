using ALKAROS.Messaging;

namespace ALKAROS.TransactionOutboxIntegration.Tests.Fixtures;

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
