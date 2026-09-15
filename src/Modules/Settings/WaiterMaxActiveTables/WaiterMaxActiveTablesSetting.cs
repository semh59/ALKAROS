namespace ALKAROS.Settings.WaiterMaxActiveTables;

using ALKAROS.Settings.TypedSettings;

/// <summary>
/// V1-SET-006: the active-order count at or above which a waiter drops
/// out of <c>SuggestedWaiterResolver</c>'s candidate pool — a hard cap on
/// top of the load-ranking it already does, for a deployment whose
/// smallest shift would otherwise keep piling every new table onto
/// whoever happens to have the lowest count even once that count is
/// already unreasonable. Default value (0) means "no cap" — the exact
/// behavior this setting replaces, so no deployment's suggestions change
/// until it opts in.
/// </summary>
public static class WaiterMaxActiveTablesSetting
{
    public const string Key = "waiter.max_active_tables";
    private const string ModuleOwner = "orders";
    private const int DefaultValue = 0;

    /// <summary>
    /// Registers the setting (value <c>0</c>, meaning "no cap") if it does
    /// not already exist. Safe to call repeatedly — a second registration
    /// is a no-op, not an error.
    /// </summary>
    public static async Task EnsureRegisteredAsync(
        ISettingsService settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            await settings.RegisterSettingAsync(
                new RegisterSettingRequest(
                    Key,
                    DefaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    SettingDataType.WholeNumber,
                    SettingScope.Global,
                    ModuleOwner,
                    Description:
                        "The active-order count at or above which a waiter is no longer " +
                        "suggested for a new table. 0 means no cap — the same behavior as " +
                        "before this setting existed."),
                cancellationToken);
        }
        catch (DuplicateSettingKeyException)
        {
            // Already registered — by an earlier boot of this same process,
            // or a previous deployment of this database. Leave it as-is; a
            // deployment's chosen value must never be silently reset.
        }
    }

    /// <summary>
    /// This deployment's active-load cap. Registers the setting (at its
    /// default, 0 = no cap) the first time it is asked, so no separate
    /// startup hook is needed.
    /// </summary>
    public static async Task<int> GetLimitAsync(
        ISettingsService settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var record = await settings.GetRecordAsync(Key, cancellationToken);
        if (record is null)
        {
            await EnsureRegisteredAsync(settings, cancellationToken);
            return DefaultValue;
        }

        return await settings.GetValueOrDefaultAsync(Key, DefaultValue, cancellationToken);
    }
}
