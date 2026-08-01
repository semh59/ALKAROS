namespace ALKAROS.SensitiveData;

/// <summary>
/// A plaintext payload with a complete field classification. Every field
/// must carry a <see cref="SensitiveCategory"/>; a payload with an
/// unclassified field is rejected at construction so that no sensitive
/// value can enter the boundary without a classification.
/// </summary>
public sealed record SensitivePayload
{
    public IReadOnlyDictionary<string, string> Fields { get; }

    public IReadOnlyDictionary<string, SensitiveCategory> Categories { get; }

    public SensitivePayload(
        IReadOnlyDictionary<string, string> fields,
        IReadOnlyDictionary<string, SensitiveCategory> categories)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(categories);

        var unclassified = fields.Keys
            .Where(field => !categories.ContainsKey(field))
            .ToList();
        if (unclassified.Count > 0)
            throw new ArgumentException(
                $"Every field must be classified; missing categories: "
                + string.Join(", ", unclassified),
                nameof(categories));

        Fields = new Dictionary<string, string>(fields, StringComparer.Ordinal);
        Categories = new Dictionary<string, SensitiveCategory>(categories, StringComparer.Ordinal);
    }
}
