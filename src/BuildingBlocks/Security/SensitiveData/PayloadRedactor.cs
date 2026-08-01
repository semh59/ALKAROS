namespace ALKAROS.SensitiveData;

/// <summary>
/// Default <see cref="IPayloadRedactor"/>: replaces every field that is not
/// <see cref="SensitiveCategory.Public"/> with a fixed mask.
/// </summary>
public sealed class PayloadRedactor : IPayloadRedactor
{
    private const string Mask = "***";

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> Redact(SensitivePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var redacted = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (field, value) in payload.Fields)
        {
            redacted[field] = payload.Categories[field] == SensitiveCategory.Public
                ? value
                : Mask;
        }

        return redacted;
    }
}
