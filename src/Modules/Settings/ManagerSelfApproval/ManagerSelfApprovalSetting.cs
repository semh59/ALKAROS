namespace ALKAROS.Settings.ManagerSelfApproval;

using ALKAROS.Settings.TypedSettings;

/// <summary>
/// whether a manager may approve their own cash-out request (and, in a later task,
/// their own refund request). Off by default: the second pair of eyes stays the rule until the business owner, who
/// may be the only manager on site, turns it on. An approval made under this setting is recorded as a self approval.
/// One setting for every approval flow - there is no second definition.
/// </summary>
public static class ManagerSelfApprovalSetting
{
    public const string Key = "approvals.manager_self_approval_enabled";
    private const string ModuleOwner = "cash";

    public static async Task EnsureRegisteredAsync(
        ISettingsService settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            await settings.RegisterSettingAsync(
                new RegisterSettingRequest(
                    Key,
                    "false",
                    SettingDataType.Toggle,
                    SettingScope.Global,
                    ModuleOwner,
                    Description:
                        "Lets a manager approve their own cash-out request. Off by default: a second person decides."),
                cancellationToken);
        }
        catch (DuplicateSettingKeyException)
        {
            // Already registered; a deployment's chosen value must never be silently reset.
        }
    }

    /// <summary>Registers the setting (off) the first time it is asked.</summary>
    public static async Task<bool> IsEnabledAsync(
        ISettingsService settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var record = await settings.GetRecordAsync(Key, cancellationToken);
        if (record is null)
        {
            await EnsureRegisteredAsync(settings, cancellationToken);
            return false;
        }

        return await settings.GetValueOrDefaultAsync(Key, false, cancellationToken);
    }
}
