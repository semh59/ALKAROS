namespace ALKAROS.Settings.BusinessIdentity;

using ALKAROS.Settings.TypedSettings;

/// <summary>
/// V1-SET-007: which of <see cref="BusinessAccentPalette"/>'s pre-vetted
/// colors the business's QR ordering pages use. Stores the palette KEY, not
/// a raw hex value — <see cref="BusinessAccentPalette.Resolve"/> always
/// turns whatever is stored (including a stale or manually-edited value
/// outside the current palette) into one of the palette's own verified
/// entries, so an unvetted color can never reach a customer page through
/// this setting.
/// </summary>
public static class BusinessAccentThemeSetting
{
    public const string Key = "business.accent_theme";
    private const string ModuleOwner = "settings";

    /// <summary>
    /// Registers the setting (at the palette's default key) if it does not
    /// already exist. Safe to call repeatedly — a second registration is a
    /// no-op, not an error.
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
                    BusinessAccentPalette.DefaultKey,
                    SettingDataType.Text,
                    SettingScope.Global,
                    ModuleOwner,
                    Description:
                        "Which BusinessAccentPalette color key the QR ordering pages use. " +
                        "Always resolved through the palette, never a raw hex value."),
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
    /// This deployment's accent color, already resolved through the
    /// palette. Registers the setting (at the default key) the first time
    /// it is asked, so no separate startup hook is needed.
    /// </summary>
    public static async Task<BusinessAccentPalette.Entry> GetThemeAsync(
        ISettingsService settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var record = await settings.GetRecordAsync(Key, cancellationToken);
        if (record is null)
        {
            await EnsureRegisteredAsync(settings, cancellationToken);
            return BusinessAccentPalette.Resolve(BusinessAccentPalette.DefaultKey);
        }

        var storedKey = await settings.GetValueOrDefaultAsync(Key, BusinessAccentPalette.DefaultKey, cancellationToken);
        return BusinessAccentPalette.Resolve(storedKey);
    }
}
