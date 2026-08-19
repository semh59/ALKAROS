namespace ALKAROS.Messaging;

/// <summary>
/// A persisted outbox message as handed to an <see cref="IOutboxDeliverySink"/>.
/// The payload envelope stays opaque; the consumer decrypts it through the
/// sensitive-data boundary.
/// </summary>
public sealed record OutboxMessage(
    Guid Id,
    string EventType,
    string AggregateType,
    Guid AggregateId,
    byte[] PayloadEnvelope,
    OutboxStatus Status,
    int AttemptCount,
    long LeaseGeneration,
    DateTimeOffset CreatedAt,
    DateTimeOffset? NextRetryAt,
    DateTimeOffset? DispatchedAt,
    string? LastError);
