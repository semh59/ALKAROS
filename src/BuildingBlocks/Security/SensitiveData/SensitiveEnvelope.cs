namespace ALKAROS.SensitiveData;

/// <summary>
/// Persistable form of a protected payload: the field classification and
/// the encrypted ciphertext. No plaintext value is part of the envelope, so
/// persistence and transport never expose sensitive fields. The timestamp
/// is captured at protection time for retention-policy hooks.
/// </summary>
public sealed record SensitiveEnvelope
{
    public IReadOnlyDictionary<string, SensitiveCategory> FieldCategories { get; init; }

    public EnvelopeCiphertext Ciphertext { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public SensitiveEnvelope(
        IReadOnlyDictionary<string, SensitiveCategory> fieldCategories,
        EnvelopeCiphertext ciphertext,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(fieldCategories);
        ArgumentNullException.ThrowIfNull(ciphertext);

        FieldCategories = new Dictionary<string, SensitiveCategory>(fieldCategories, StringComparer.Ordinal);
        Ciphertext = ciphertext;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Serializes the envelope for persistence. The bytes contain only
    /// classification metadata and ciphertext — never plaintext fields.
    /// </summary>
    public byte[] ToPersistenceBytes() =>
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(this);

    /// <summary>
    /// Restores an envelope previously produced by <see cref="ToPersistenceBytes"/>.
    /// </summary>
    /// <exception cref="SensitiveDataException">
    /// The bytes are not a valid serialized envelope.
    /// </exception>
    public static SensitiveEnvelope FromPersistenceBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<SensitiveEnvelope>(bytes)
                ?? throw new SensitiveDataException(
                    "Payload failed to deserialize; it was not written by this boundary.");
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new SensitiveDataException(
                "Payload failed to deserialize; it was not written by this boundary.",
                exception);
        }
    }
}
