namespace ALKAROS.Orders.SubmitOrder;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class SubmitOrderResponseSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static byte[] Serialize(SubmitOrderResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return JsonSerializer.SerializeToUtf8Bytes(result, Options);
    }

    public static SubmitOrderResult Deserialize(byte[] utf8Bytes, bool isReplay)
    {
        ArgumentNullException.ThrowIfNull(utf8Bytes);
        var raw = JsonSerializer.Deserialize<SubmitOrderResult>(utf8Bytes, Options)
            ?? throw new InvalidOperationException("Failed to deserialize SubmitOrderResult from stored response envelope.");
        return raw with { IsReplay = isReplay };
    }
}
