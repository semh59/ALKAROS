using ALKAROS.Settings.TypedSettings;

namespace ALKAROS.Host.Experience.Settings;

public sealed record UpdateSettingValueV1(string NewValue, long ExpectedRowVersion, string? Reason = null);

public sealed record SettingRecordV1(
    Guid SettingId,
    string Key,
    string Value,
    string DataType,
    string Scope,
    string ModuleOwner,
    string? Description,
    bool RequiresRestart,
    bool Active,
    DateTimeOffset UpdatedAt,
    long RowVersion)
{
    public static SettingRecordV1 From(SettingRecord value)
        => new(value.SettingId, value.Key, value.Value, value.DataType.ToString(), value.Scope.ToString(),
            value.ModuleOwner, value.Description, value.RequiresRestart, value.Active, value.UpdatedAt, value.RowVersion);
}

public sealed record SettingHistoryRecordV1(
    Guid SettingHistoryId, Guid SettingId, string? OldValue, string NewValue,
    string? Reason, Guid? ChangedBy, DateTimeOffset ChangedAt)
{
    public static SettingHistoryRecordV1 From(SettingHistoryRecord value)
        => new(value.SettingHistoryId, value.SettingId, value.OldValue, value.NewValue,
            value.Reason, value.ChangedBy, value.ChangedAt);
}

