namespace ALKAROS.Messaging;

/// <summary>
/// A persisted inbox message as handed to an <see cref="IInboxHandler"/>.
/// The payload envelope stays opaque; the consumer decrypts it through the
/// sensitive-data boundary.
/// </summary>
/// <remarks>
/// The same logical message can be delivered more than once (retry or
/// expired-lease redelivery). <see cref="AttemptCount"/> is the number of
/// failed attempts already recorded before this delivery (0 on the first
/// delivery), <see cref="LastError"/> is the most recent failure text, and
/// <see cref="Status"/> is the persisted state observed at claim time.
/// Handlers deduplicate their side effects by
/// (<see cref="Source"/>, <see cref="ExternalEventId"/>).
/// </remarks>
public sealed record InboxMessage(
    Guid Id,
    string Source,
    string ExternalEventId,
    byte[] PayloadEnvelope,
    InboxStatus Status,
    int AttemptCount,
    long LeaseGeneration,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProcessedAt,
    string? LastError);
