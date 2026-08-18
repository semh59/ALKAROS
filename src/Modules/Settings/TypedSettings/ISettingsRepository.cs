namespace ALKAROS.Settings.TypedSettings;

/// <summary>
/// Repository interface for storing, reading, and auditing module-owned typed settings (V1-SET-001, PDF:III.27).
/// </summary>
public interface ISettingsRepository
{
    /// <summary>
    /// Retrieves a setting by its unique key.
    /// </summary>
    Task<SettingRecord?> GetByKeyAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all settings owned by a specific module.
    /// </summary>
    Task<IReadOnlyList<SettingRecord>> GetByModuleOwnerAsync(
        string moduleOwner,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all currently active settings.
    /// </summary>
    Task<IReadOnlyList<SettingRecord>> GetAllActiveAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the append-only modification history for a given setting.
    /// </summary>
    Task<IReadOnlyList<SettingHistoryRecord>> GetHistoryAsync(
        Guid settingId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new setting, validates data type, and writes the initial history entry.
    /// </summary>
    Task<SettingRecord> RegisterSettingAsync(
        RegisterSettingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the value of an existing setting with optimistic concurrency and appends to history.
    /// </summary>
    Task<SettingRecord> UpdateSettingAsync(
        UpdateSettingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a setting as inactive (active = false) without physically deleting it and appends to history.
    /// </summary>
    Task<SettingRecord> DeactivateSettingAsync(
        DeactivateSettingRequest request,
        CancellationToken cancellationToken = default);
}
