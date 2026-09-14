namespace ALKAROS.Settings.KitchenDenseModeThreshold;

using ALKAROS.Settings.TypedSettings;

/// <summary>
/// V1-SET-005: the open-item count at or above which the Kitchen screen
/// switches to its own dense/compact layout on its own
/// (KitchenOperationsWorkspace.tsx's AUTO_DENSE_OPEN_ITEM_THRESHOLD, until
/// this task hardcoded at 9). Kitchens differ in size and volume too much
/// for one fixed number to fit every deployment — a small counter kitchen
/// might want dense mode much sooner, a large one much later. Default
/// value (9) matches the constant this setting replaces exactly, so no
/// deployment's screen changes behavior until it opts in.
/// </summary>
public static class KitchenDenseModeThresholdSetting
{
    public const string Key = "kitchen.dense_mode_threshold";
    private const string ModuleOwner = "kitchen";
    private const int DefaultValue = 9;

    /// <summary>
    /// Registers the setting (value <c>9</c>) if it does not already exist.
    /// Safe to call repeatedly — a second registration is a no-op, not an
    /// error.
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
                        "The open-item count at or above which the Kitchen screen automatically " +
                        "switches to its dense/compact layout. Defaults to 9 — the same number " +
                        "the screen used before this became configurable."),
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
    /// This deployment's dense-mode threshold. Registers the setting (at
    /// its default, 9) the first time it is asked, so no separate startup
    /// hook is needed.
    /// </summary>
    public static async Task<int> GetThresholdAsync(
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
