namespace ALKAROS.Messaging;

/// <summary>
/// An external callback waiting for inbox persistence (V0-ARC-003 §2).
/// The payload travels as an already-protected sensitive envelope; the
/// inbox boundary never inspects or logs its plaintext.
/// </summary>
public sealed record InboxEnvelope
{
    private const int MaxSourceLength = 100;
    private const int MaxExternalEventIdLength = 200;

    public string Source { get; }

    public string ExternalEventId { get; }

    public byte[] PayloadEnvelope { get; }

    public InboxEnvelope(string source, string externalEventId, byte[] payloadEnvelope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalEventId);
        ArgumentNullException.ThrowIfNull(payloadEnvelope);
        if (source.Length > MaxSourceLength || externalEventId.Length > MaxExternalEventIdLength)
            throw new ArgumentException(
                $"Source must not exceed {MaxSourceLength} and external event id "
                + $"{MaxExternalEventIdLength} characters.",
                nameof(externalEventId));

        Source = source;
        ExternalEventId = externalEventId;
        PayloadEnvelope = payloadEnvelope;
    }
}
