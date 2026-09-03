using System.Text.Json;

namespace ALKAROS.IntegrationContracts;

/// <summary>
/// Serializes integration event payloads for the outbox. These events carry
/// only aggregate identifiers and timestamps — no personal or financial data —
/// so the payload is plain UTF-8 JSON, not a protected sensitive envelope. An
/// event that ever needs to carry sensitive data must wrap it through the
/// SensitiveData boundary before it reaches the outbox.
/// </summary>
public static class IntegrationEventSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static byte[] Serialize<T>(T @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        return JsonSerializer.SerializeToUtf8Bytes(@event, Options);
    }

    public static T Deserialize<T>(ReadOnlySpan<byte> payload)
        => JsonSerializer.Deserialize<T>(payload, Options)
           ?? throw new InvalidOperationException(
               $"Integration event payload deserialized to null for {typeof(T).Name}.");
}
