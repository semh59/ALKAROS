using System.Text.Json;

namespace ALKAROS.IntegrationContracts;

/// <summary>
/// Serializes integration event payloads for the outbox as plain UTF-8 JSON,
/// not a protected sensitive envelope. Most events carry only identifiers and
/// timestamps, but a few carry customer free text (the QR order note), which
/// is stored here unencrypted until the outbox purge deletes the row. An event
/// that needs stronger protection must wrap its payload through the
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
