namespace ALKAROS.Messaging;

/// <summary>
/// A domain event waiting for outbox dispatch (V0-ARC-003 §3). The payload
/// travels as an already-protected sensitive envelope; the outbox boundary
/// never inspects or logs its plaintext.
/// </summary>
public sealed record OutboxEnvelope
{
    private const int MaxTypeLength = 100;

    public string EventType { get; }

    public string AggregateType { get; }

    public Guid AggregateId { get; }

    public byte[] PayloadEnvelope { get; }

    public OutboxEnvelope(
        string eventType,
        string aggregateType,
        Guid aggregateId,
        byte[] payloadEnvelope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentNullException.ThrowIfNull(payloadEnvelope);
        if (eventType.Length > MaxTypeLength || aggregateType.Length > MaxTypeLength)
            throw new ArgumentException(
                $"Event type and aggregate type must not exceed {MaxTypeLength} characters.",
                nameof(aggregateType));

        EventType = eventType;
        AggregateType = aggregateType;
        AggregateId = aggregateId;
        PayloadEnvelope = payloadEnvelope;
    }
}
