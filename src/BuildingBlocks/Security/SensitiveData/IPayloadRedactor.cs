namespace ALKAROS.SensitiveData;

/// <summary>
/// Produces a log-safe representation of a payload: every non-public field
/// is replaced by a fixed mask so that no sensitive value can reach a log
/// line. Public fields are kept readable.
/// </summary>
public interface IPayloadRedactor
{
    /// <summary>
    /// Returns the field map of <paramref name="payload"/> with every
    /// non-public field masked.
    /// </summary>
    IReadOnlyDictionary<string, string> Redact(SensitivePayload payload);
}
