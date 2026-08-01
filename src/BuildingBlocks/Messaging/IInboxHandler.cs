namespace ALKAROS.Messaging;

/// <summary>
/// The consumer of a processed inbox message. Returning <c>false</c> (or
/// throwing) counts as a failed attempt and advances the poison bookkeeping;
/// the message is redelivered until the dead-letter threshold.
/// </summary>
public interface IInboxHandler
{
    Task<bool> HandleAsync(InboxMessage message, CancellationToken cancellationToken);
}
