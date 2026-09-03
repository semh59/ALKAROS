using ALKAROS.IntegrationContracts;

namespace ALKAROS.Messaging;

/// <summary>
/// The outbox delivery sink for in-process integration events: hands each
/// claimed message to every <see cref="IIntegrationEventConsumer"/> that
/// <see cref="IIntegrationEventConsumer.CanHandle"/> its type. A consumer that
/// throws fails the whole message; the dispatcher retries it with backoff and
/// every consumer runs again, so consumers must be idempotent (at-least-once,
/// V0-ARC-003 §3). A message no consumer handles is still acknowledged so it
/// does not exhaust its retries against nobody.
/// </summary>
public sealed class OutboxFanoutSink : IOutboxDeliverySink
{
    private readonly IReadOnlyList<IIntegrationEventConsumer> _consumers;

    public OutboxFanoutSink(IEnumerable<IIntegrationEventConsumer> consumers)
    {
        ArgumentNullException.ThrowIfNull(consumers);
        _consumers = consumers.ToList();
    }

    public async Task<bool> HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        foreach (var consumer in _consumers)
        {
            if (consumer.CanHandle(message.EventType))
            {
                await consumer.HandleAsync(message.EventType, message.PayloadEnvelope, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return true;
    }
}
