namespace ALKAROS.Messaging;

/// <summary>
/// A persisted inbox message as handed to an <see cref="IInboxHandler"/>.
/// The payload envelope stays opaque; the consumer decrypts it through the
/// sensitive-data boundary.
/// </summary>
public sealed record InboxMessage(
    Guid Id,
    string Source,
    string ExternalEventId,
    byte[] PayloadEnvelope,
    InboxStatus Status,
    int AttemptCount,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProcessedAt,
    string? LastError);
