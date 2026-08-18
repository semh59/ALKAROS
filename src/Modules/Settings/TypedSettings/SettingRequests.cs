namespace ALKAROS.Settings.TypedSettings;

/// <summary>
/// Command request to register a new module-owned setting (V1-SET-001).
/// </summary>
public sealed record RegisterSettingRequest(
    string Key,
    string Value,
    SettingDataType DataType,
    SettingScope Scope,
    string ModuleOwner,
    string? Description = null,
    bool RequiresRestart = false,
    Guid? RegisteredBy = null,
    string? Reason = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Key))
            throw new ArgumentException("Setting key cannot be null, empty, or whitespace.", nameof(Key));

        if (Value is null)
            throw new ArgumentNullException(nameof(Value), "Setting value cannot be null.");

        if (string.IsNullOrWhiteSpace(ModuleOwner))
            throw new ArgumentException("Module owner cannot be null, empty, or whitespace.", nameof(ModuleOwner));
    }
}

/// <summary>
/// Command request to update the value of an existing setting with optimistic concurrency check (V1-SET-001).
/// </summary>
public sealed record UpdateSettingRequest(
    string Key,
    string NewValue,
    long ExpectedRowVersion,
    Guid? UpdatedBy = null,
    string? Reason = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Key))
            throw new ArgumentException("Setting key cannot be null, empty, or whitespace.", nameof(Key));

        if (NewValue is null)
            throw new ArgumentNullException(nameof(NewValue), "New value cannot be null.");

        if (ExpectedRowVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(ExpectedRowVersion), ExpectedRowVersion, "Expected row version must be positive.");
    }
}

/// <summary>
/// Command request to deactivate an active setting without physical deletion (V1-SET-001).
/// </summary>
public sealed record DeactivateSettingRequest(
    string Key,
    long ExpectedRowVersion,
    Guid? DeactivatedBy = null,
    string? Reason = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Key))
            throw new ArgumentException("Setting key cannot be null, empty, or whitespace.", nameof(Key));

        if (ExpectedRowVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(ExpectedRowVersion), ExpectedRowVersion, "Expected row version must be positive.");
    }
}
