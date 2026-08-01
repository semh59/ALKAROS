namespace ALKAROS.SensitiveData;

/// <summary>
/// Classification of a payload field. A payload is only accepted by the
/// boundary when every field carries one of these categories; fields that
/// are not <see cref="Public"/> are never persisted or logged in plaintext
/// and are masked in every log-facing representation.
/// </summary>
public enum SensitiveCategory
{
    /// <summary>
    /// Field may appear in plaintext in persistence and logs.
    /// </summary>
    Public = 0,

    /// <summary>
    /// Personal data (for example name, phone, email, address).
    /// </summary>
    Pii = 1,

    /// <summary>
    /// Payment and financial data (for example card data, amounts tied to
    /// payment instruments, fiscal payloads).
    /// </summary>
    Payment = 2,

    /// <summary>
    /// Credential material (tokens, secrets, keys).
    /// </summary>
    Credential = 3,
}
