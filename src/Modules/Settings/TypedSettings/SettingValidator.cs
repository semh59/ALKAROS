using System.Globalization;
using System.Text.Json;
using System.Xml;

namespace ALKAROS.Settings.TypedSettings;

/// <summary>
/// Domain validator contract for setting keys and typed values (V1-SET-001, PDF:III.27.1, V0-ARC-005).
/// </summary>
public interface ISettingValidator
{
    /// <summary>
    /// Validates that a setting key does not violate the Secret Storage Ban.
    /// </summary>
    void ValidateKey(string settingKey);

    /// <summary>
    /// Validates that a value strictly conforms to the expected SettingDataType.
    /// </summary>
    void ValidateValue(string settingKey, string value, SettingDataType dataType);
}

/// <summary>
/// Domain validator implementation for setting keys and values (V1-SET-001).
/// </summary>
public sealed class SettingValidator : ISettingValidator
{
    private static readonly string[] BannedSecretKeywords =
    {
        "secret",
        "password",
        "token",
        "api_key",
        "apikey",
        "private_key",
        "privatekey",
        "credential",
        "credentials",
        "auth_token",
        "authtoken",
        "access_token",
        "accesstoken",
        "client_secret",
        "clientsecret"
    };

    public void ValidateKey(string settingKey)
    {
        if (string.IsNullOrWhiteSpace(settingKey))
            throw new ArgumentException("Setting key cannot be null, empty, or whitespace.", nameof(settingKey));

        var lowerKey = settingKey.ToLowerInvariant();
        foreach (var keyword in BannedSecretKeywords)
        {
            if (lowerKey.Contains(keyword, StringComparison.Ordinal))
            {
                throw new SecretSettingsStorageBanException(
                    settingKey,
                    $"Setting key contains prohibited secret keyword '{keyword}'. Secrets and credentials must use the secure secret store (V0-ARC-005, PDF:II.11).");
            }
        }
    }

    public void ValidateValue(string settingKey, string value, SettingDataType dataType)
    {
        ArgumentNullException.ThrowIfNull(value);

        switch (dataType)
        {
            case SettingDataType.Text:
                // Any valid non-null string is permitted
                break;

            case SettingDataType.WholeNumber:
                if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    throw new SettingTypeValidationException(
                        settingKey,
                        dataType,
                        value,
                        "Value must be a valid integer (64-bit integer representation in invariant culture).");
                }
                break;

            case SettingDataType.PreciseNumber:
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                {
                    throw new SettingTypeValidationException(
                        settingKey,
                        dataType,
                        value,
                        "Value must be a valid decimal number in invariant culture format (e.g. '12.50').");
                }
                break;

            case SettingDataType.Toggle:
                if (!bool.TryParse(value, out _))
                {
                    throw new SettingTypeValidationException(
                        settingKey,
                        dataType,
                        value,
                        "Value must be a valid boolean ('true' or 'false').");
                }
                break;

            case SettingDataType.Json:
                try
                {
                    using var doc = JsonDocument.Parse(value);
                }
                catch (JsonException ex)
                {
                    throw new SettingTypeValidationException(
                        settingKey,
                        dataType,
                        value,
                        $"Value must be a valid JSON document: {ex.Message}");
                }
                break;

            case SettingDataType.Duration:
                if (!TryParseDuration(value, out _))
                {
                    throw new SettingTypeValidationException(
                        settingKey,
                        dataType,
                        value,
                        "Value must be a valid duration (e.g. '00:15:00' or ISO-8601 'PT15M').");
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "Unsupported SettingDataType.");
        }
    }

    private static bool TryParseDuration(string value, out TimeSpan timeSpan)
    {
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out timeSpan))
            return true;

        try
        {
            timeSpan = XmlConvert.ToTimeSpan(value);
            return true;
        }
        catch
        {
            timeSpan = default;
            return false;
        }
    }
}
