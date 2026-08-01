namespace ALKAROS.Messaging;

/// <summary>
/// The delivery sink of an outbox message. Returning <c>false</c> (or
/// throwing) counts as a failed delivery; the dispatcher retries with
/// exponential backoff and moves the message to dead-letter after three
/// attempts (at-least-once delivery, V0-ARC-003 §3).
/// </summary>
public interface IOutboxDeliverySink
{
    Task<bool> HandleAsync(OutboxMessage message, CancellationToken cancellationToken);
}
