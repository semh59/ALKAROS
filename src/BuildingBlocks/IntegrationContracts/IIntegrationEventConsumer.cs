namespace ALKAROS.IntegrationContracts;

/// <summary>
/// A module's subscription to integration events delivered from the outbox.
/// The fan-out sink hands every claimed message to every registered consumer
/// that <see cref="CanHandle"/> its type. Delivery is at-least-once: a consumer
/// MUST tolerate repeated delivery of the same event and MUST NOT produce a
/// second side effect for a redelivery (V0-ARC-003 §3). A consumer changes
/// only its own module's state in response to another module's event
/// (V0-ARC-001 §2).
/// </summary>
public interface IIntegrationEventConsumer
{
    /// <summary>True when this consumer wants <paramref name="eventType"/>.</summary>
    bool CanHandle(string eventType);

    /// <summary>
    /// Applies the event. Throwing or failing counts as a failed delivery; the
    /// dispatcher retries with backoff and dead-letters after the threshold.
    /// </summary>
    Task HandleAsync(string eventType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
}
