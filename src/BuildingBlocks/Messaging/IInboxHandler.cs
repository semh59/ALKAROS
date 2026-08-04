namespace ALKAROS.Messaging;

/// <summary>
/// The consumer of a processed inbox message. Messages are delivered at
/// least once: the same message can be handed to the handler multiple times
/// (scheduled retries after a failed attempt, or a new claim after the
/// previous lease expired on a crashed worker). Returning <c>false</c> (or
/// throwing) counts as a failed attempt and advances the poison bookkeeping;
/// the message is redelivered until the dead-letter threshold.
/// </summary>
/// <remarks>
/// Idempotency contract: an implementation MUST tolerate repeated delivery of
/// the same message and MUST NOT produce a second side effect for a
/// redelivery. Duplicate detection uses the idempotency key
/// (<see cref="InboxMessage.Source"/> + <see cref="InboxMessage.ExternalEventId"/>);
/// the number of failed attempts already recorded before a delivery is
/// exposed through <see cref="InboxMessage.AttemptCount"/>.
/// </remarks>
public interface IInboxHandler
{
    Task<bool> HandleAsync(InboxMessage message, CancellationToken cancellationToken);
}
