using System.Globalization;
using System.Text.Json;
using System.Xml;

namespace ALKAROS.Settings.TypedSettings;

/// <summary>
/// Domain service interface providing strongly-typed access and management for module-owned settings (V1-SET-001).
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Reads and converts a setting value to type <typeparamref name="T"/>. Throws if not found or type mismatch.
    /// </summary>
    Task<T> GetValueAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a setting value converted to type <typeparamref name="T"/>, returning <paramref name="defaultValue"/> if not found or inactive.
    /// </summary>
    Task<T> GetValueOrDefaultAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the raw setting domain record.
    /// </summary>
    Task<SettingRecord?> GetRecordAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all settings for a module owner.
    /// </summary>
    Task<IReadOnlyList<SettingRecord>> GetByModuleOwnerAsync(string moduleOwner, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all active settings.
    /// </summary>
    Task<IReadOnlyList<SettingRecord>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new setting with type validation.
    /// </summary>
    Task<SettingRecord> RegisterSettingAsync(RegisterSettingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a strongly-typed setting value with optimistic concurrency check.
    /// </summary>
    Task<SettingRecord> SetValueAsync<T>(
        string key,
        T value,
        long expectedRowVersion,
        Guid? updatedBy = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a setting without physical deletion.
    /// </summary>
    Task<SettingRecord> DeactivateAsync(
        string key,
        long expectedRowVersion,
        Guid? deactivatedBy = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the change audit history for a setting.
    /// </summary>
    Task<IReadOnlyList<SettingHistoryRecord>> GetHistoryAsync(Guid settingId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of <see cref="ISettingsService"/> with strongly-typed conversion and validation (V1-SET-001).
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly ISettingsRepository _repository;

    public SettingsService(ISettingsRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<T> GetValueAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var record = await _repository.GetByKeyAsync(key, cancellationToken);
        if (record is null || !record.Active)
            throw new SettingNotFoundException(key);

        return ConvertValue<T>(record.Key, record.Value, record.DataType);
    }

    public async Task<T> GetValueOrDefaultAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
    {
        var record = await _repository.GetByKeyAsync(key, cancellationToken);
        if (record is null || !record.Active)
            return defaultValue;

        try
        {
            return ConvertValue<T>(record.Key, record.Value, record.DataType);
        }
        catch (SettingTypeValidationException)
        {
            return defaultValue;
        }
    }

    public Task<SettingRecord?> GetRecordAsync(string key, CancellationToken cancellationToken = default)
    {
        return _repository.GetByKeyAsync(key, cancellationToken);
    }

    public Task<IReadOnlyList<SettingRecord>> GetByModuleOwnerAsync(string moduleOwner, CancellationToken cancellationToken = default)
    {
        return _repository.GetByModuleOwnerAsync(moduleOwner, cancellationToken);
    }

    public Task<IReadOnlyList<SettingRecord>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllActiveAsync(cancellationToken);
    }

    public Task<SettingRecord> RegisterSettingAsync(RegisterSettingRequest request, CancellationToken cancellationToken = default)
    {
        return _repository.RegisterSettingAsync(request, cancellationToken);
    }

    public Task<SettingRecord> SetValueAsync<T>(
        string key,
        T value,
        long expectedRowVersion,
        Guid? updatedBy = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var serializedValue = SerializeValue(value);
        var request = new UpdateSettingRequest(key, serializedValue, expectedRowVersion, updatedBy, reason);
        return _repository.UpdateSettingAsync(request, cancellationToken);
    }

    public Task<SettingRecord> DeactivateAsync(
        string key,
        long expectedRowVersion,
        Guid? deactivatedBy = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var request = new DeactivateSettingRequest(key, expectedRowVersion, deactivatedBy, reason);
        return _repository.DeactivateSettingAsync(request, cancellationToken);
    }

    public Task<IReadOnlyList<SettingHistoryRecord>> GetHistoryAsync(Guid settingId, CancellationToken cancellationToken = default)
    {
        return _repository.GetHistoryAsync(settingId, cancellationToken);
    }

    private static T ConvertValue<T>(string key, string value, SettingDataType dataType)
    {
        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        try
        {
            if (targetType == typeof(string))
                return (T)(object)value;

            if (targetType == typeof(int))
                return (T)(object)int.Parse(value, CultureInfo.InvariantCulture);

            if (targetType == typeof(long))
                return (T)(object)long.Parse(value, CultureInfo.InvariantCulture);

            if (targetType == typeof(decimal))
                return (T)(object)decimal.Parse(value, CultureInfo.InvariantCulture);

            if (targetType == typeof(double))
                return (T)(object)double.Parse(value, CultureInfo.InvariantCulture);

            if (targetType == typeof(bool))
                return (T)(object)bool.Parse(value);

            if (targetType == typeof(TimeSpan))
            {
                if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var ts))
                    return (T)(object)ts;
                return (T)(object)XmlConvert.ToTimeSpan(value);
            }

            if (dataType == SettingDataType.Json)
            {
                var deserialized = JsonSerializer.Deserialize<T>(value);
                if (deserialized is not null)
                    return deserialized;
            }

            return (T)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            throw new SettingTypeValidationException(key, dataType, value, $"Cannot convert value to {typeof(T).Name}: {ex.Message}");
        }
    }

    private static string SerializeValue<T>(T value)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value), "Cannot set null setting value.");

        if (value is string s)
            return s;

        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture);

        if (value is bool b)
            return b ? "true" : "false";

        if (value is TimeSpan ts)
            return ts.ToString("c", CultureInfo.InvariantCulture);

        return JsonSerializer.Serialize(value);
    }
}
