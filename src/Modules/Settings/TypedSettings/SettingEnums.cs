namespace ALKAROS.Settings.TypedSettings;

/// <summary>
/// Supported strongly-typed data types for module and system settings (V1-SET-001, PDF:III.27.1).
/// </summary>
public enum SettingDataType
{
    Text,
    WholeNumber,
    PreciseNumber,
    Toggle,
    Json,
    Duration
}

/// <summary>
/// Visibility and resolution scope of a setting (V1-SET-001, PDF:III.27.1, V0-ARC-005).
/// </summary>
public enum SettingScope
{
    Global,
    Module,
    Device,
    Tenant
}

/// <summary>
/// Extension methods for mapping <see cref="SettingDataType"/> to/from database strings.
/// </summary>
public static class SettingDataTypeMapper
{
    public static string ToDbString(SettingDataType type) => type switch
    {
        SettingDataType.Text => "String",
        SettingDataType.WholeNumber => "Integer",
        SettingDataType.PreciseNumber => "Decimal",
        SettingDataType.Toggle => "Boolean",
        SettingDataType.Json => "Json",
        SettingDataType.Duration => "Duration",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported SettingDataType.")
    };

    public static SettingDataType FromDbString(string value) => value.ToUpperInvariant() switch
    {
        "STRING" or "TEXT" => SettingDataType.Text,
        "INTEGER" or "WHOLENUMBER" or "INT" => SettingDataType.WholeNumber,
        "DECIMAL" or "PRECISENUMBER" or "NUMERIC" => SettingDataType.PreciseNumber,
        "BOOLEAN" or "TOGGLE" or "BOOL" => SettingDataType.Toggle,
        "JSON" => SettingDataType.Json,
        "DURATION" or "TIMESPAN" => SettingDataType.Duration,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, $"Unknown setting data type: {value}")
    };
}
