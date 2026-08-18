namespace ALKAROS.Settings.TypedSettings;

/// <summary>
/// Immutable domain record representing a module-owned setting (V1-SET-001, PDF:III.27.1).
/// </summary>
public sealed record SettingRecord(
    Guid SettingId,
    string Key,
    string Value,
    SettingDataType DataType,
    SettingScope Scope,
    string ModuleOwner,
    string? Description,
    bool RequiresRestart,
    bool Active,
    DateTimeOffset UpdatedAt,
    long RowVersion)
{
    public bool IsActive => Active;
}

/// <summary>
/// Immutable domain record representing an append-only change history entry for a setting (V1-SET-001, PDF:III.27.2).
/// </summary>
public sealed record SettingHistoryRecord(
    Guid SettingHistoryId,
    Guid SettingId,
    string? OldValue,
    string NewValue,
    string? Reason,
    Guid? ChangedBy,
    DateTimeOffset ChangedAt);
