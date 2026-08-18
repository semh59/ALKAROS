namespace ALKAROS.Settings.TypedSettings;

/// <summary>
/// Base exception for domain settings errors (V1-SET-001).
/// </summary>
public abstract class SettingsException : Exception
{
    protected SettingsException(string message) : base(message) { }
    protected SettingsException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a requested setting key is not found in the settings registry.
/// </summary>
public sealed class SettingNotFoundException : SettingsException
{
    public SettingNotFoundException(string settingKey)
        : base($"Setting with key '{settingKey}' was not found.")
    {
        SettingKey = settingKey;
    }

    public string SettingKey { get; }
}

/// <summary>
/// Thrown when an attempt is made to store a value that does not match the registered SettingDataType.
/// </summary>
public sealed class SettingTypeValidationException : SettingsException
{
    public SettingTypeValidationException(string settingKey, SettingDataType expectedType, string actualValue, string validationError)
        : base($"Value '{actualValue}' is invalid for setting '{settingKey}' of type '{expectedType}': {validationError}")
    {
        SettingKey = settingKey;
        ExpectedType = expectedType;
        ActualValue = actualValue;
        ValidationError = validationError;
    }

    public string SettingKey { get; }
    public SettingDataType ExpectedType { get; }
    public string ActualValue { get; }
    public string ValidationError { get; }
}

/// <summary>
/// Thrown when a setting key is classified as a secret, credential, token, or password (PDF:II.11, V0-ARC-005).
/// </summary>
public sealed class SecretSettingsStorageBanException : SettingsException
{
    public SecretSettingsStorageBanException(string settingKey, string reason)
        : base($"Setting key '{settingKey}' violates the Secret Storage Ban: {reason}. Credentials, tokens and keys must reside in the secret store.")
    {
        SettingKey = settingKey;
        Reason = reason;
    }

    public string SettingKey { get; }
    public string Reason { get; }
}

/// <summary>
/// Thrown when an optimistic concurrency conflict occurs during setting updates.
/// </summary>
public sealed class SettingConcurrencyException : SettingsException
{
    public SettingConcurrencyException(string settingKey, long expectedVersion, long actualVersion)
        : base($"Concurrency conflict on setting '{settingKey}': expected row version {expectedVersion}, actual {actualVersion}.")
    {
        SettingKey = settingKey;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    public string SettingKey { get; }
    public long ExpectedVersion { get; }
    public long ActualVersion { get; }
}

/// <summary>
/// Thrown when attempting to register a setting key that already exists.
/// </summary>
public sealed class DuplicateSettingKeyException : SettingsException
{
    public DuplicateSettingKeyException(string settingKey)
        : base($"Setting key '{settingKey}' is already registered.")
    {
        SettingKey = settingKey;
    }

    public string SettingKey { get; }
}
