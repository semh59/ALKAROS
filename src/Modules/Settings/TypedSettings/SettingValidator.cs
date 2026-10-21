using System.Globalization;
using System.Text.Json;
using System.Xml;
using ALKAROS.Settings.BusinessIdentity;
using ALKAROS.Settings.KitchenDenseModeThreshold;
using ALKAROS.Settings.QrOrderExpiry;
using ALKAROS.Settings.WaiterMaxActiveTables;

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
    // Compared against the key with every separator removed, so api_key, api-key and x.apikey are one spelling.
    private static readonly string[] BannedSecretKeywords =
    {
        "secret",
        "password",
        "passwd",
        "pwd",
        "passphrase",
        "token",
        "apikey",
        "privatekey",
        "credential",
        "authtoken",
        "accesstoken",
        "clientsecret"
    };

    private const int MaxTextLength = 2000;
    private const int MaxJsonLength = 16000;

    // A value outside its key's range (a zero QR timeout rejects every pending QR order at once) is refused at the door.
    private static readonly Dictionary<string, (Func<string, bool> IsValid, string Message)> KeyRules = new()
    {
        [WaiterMaxActiveTablesSetting.Key] = (v => WholeNumberBetween(v, 0, 50), "Garson başına masa sınırı 0 ile 50 arasında olmalı (0 = sınırsız)."),
        [KitchenDenseModeThresholdSetting.Key] = (v => WholeNumberBetween(v, 1, 99), "Mutfak yoğun mod eşiği 1 ile 99 arasında olmalı."),
        [QrOrderExpirySetting.Key] = (v => DurationBetween(v, TimeSpan.FromMinutes(1), TimeSpan.FromHours(24)), "QR sipariş onay süresi 1 dakika ile 24 saat arasında olmalı."),
        [BusinessNameSetting.Key] = (v => v.Length <= 80 && !v.Any(char.IsControl), "İşletme adı en çok 80 karakter olabilir ve denetim karakteri içeremez."),
    };

    public void ValidateKey(string settingKey)
    {
        if (string.IsNullOrWhiteSpace(settingKey))
            throw new ArgumentException("Setting key cannot be null, empty, or whitespace.", nameof(settingKey));

        var lowerKey = new string(settingKey.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
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
                if (value.Length > MaxTextLength)
                    throw new SettingValueRangeException(settingKey, $"Değer en çok {MaxTextLength} karakter olabilir.");
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
                if (value.Length > MaxJsonLength)
                    throw new SettingValueRangeException(settingKey, $"Değer en çok {MaxJsonLength} karakter olabilir.");
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

        if (KeyRules.TryGetValue(settingKey, out var rule) && !rule.IsValid(value))
            throw new SettingValueRangeException(settingKey, rule.Message);
    }

    // "c" format only ("00:15:00", "1.02:00:00"): even that format reads a bare "5" as five days, hence the colon rule. A duration is never zero or negative.
    private static bool TryParseDuration(string value, out TimeSpan timeSpan)
    {
        timeSpan = default;
        if (!value.Contains(':') || !TimeSpan.TryParseExact(value, "c", CultureInfo.InvariantCulture, out timeSpan))
        {
            try
            {
                timeSpan = value.StartsWith('P') ? XmlConvert.ToTimeSpan(value) : default;
            }
            catch
            {
                timeSpan = default;
            }
        }

        return timeSpan > TimeSpan.Zero;
    }

    private static bool WholeNumberBetween(string value, long min, long max)
        => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number >= min && number <= max;

    private static bool DurationBetween(string value, TimeSpan min, TimeSpan max)
        => TryParseDuration(value, out var duration) && duration >= min && duration <= max;
}

/// <summary>A value that has the right type but falls outside what the setting allows; <see cref="UserMessage"/> is Turkish and safe to show.</summary>
public sealed class SettingValueRangeException : SettingsException
{
    public SettingValueRangeException(string settingKey, string userMessage)
        : base($"Value for setting '{settingKey}' is outside its allowed range.")
    {
        SettingKey = settingKey;
        UserMessage = userMessage;
    }

    public string SettingKey { get; }
    public string UserMessage { get; }
}

/// <summary>A deactivated setting can be neither changed nor deactivated again.</summary>
public sealed class SettingInactiveException : SettingsException
{
    public SettingInactiveException(string settingKey)
        : base($"Setting '{settingKey}' is deactivated.")
    {
        SettingKey = settingKey;
    }

    public string SettingKey { get; }
}
