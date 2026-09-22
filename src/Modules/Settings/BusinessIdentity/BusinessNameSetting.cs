namespace ALKAROS.Settings.BusinessIdentity;

using ALKAROS.Settings.TypedSettings;

/// <summary>
/// V1-SET-007: the business's own display name, shown on the QR ordering
/// pages instead of no name at all (2026-09-19 product decision — the
/// customer page belongs to the restaurant, not the POS vendor; ALKAROS's
/// own name/logo is never shown there — see the accompanying task file for
/// the real competitor evidence this was checked against). Empty string
/// (the default) means "not set" — the QR pages keep their existing
/// unbranded copy; no deployment's customer pages change until a manager
/// sets this. Mirrors WaiterMaxActiveTablesSetting's (V1-SET-006) exact
/// shape.
/// </summary>
public static class BusinessNameSetting
{
    public const string Key = "business.name";
    private const string ModuleOwner = "settings";
    private const string DefaultValue = "";

    /// <summary>
    /// Registers the setting (empty value) if it does not already exist.
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
                    DefaultValue,
                    SettingDataType.Text,
                    SettingScope.Global,
                    ModuleOwner,
                    Description:
                        "The business's own display name, shown on the QR ordering pages. " +
                        "Empty means not set — the pages show no business name."),
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
    /// This deployment's business name. Registers the setting (empty) the
    /// first time it is asked, so no separate startup hook is needed.
    /// </summary>
    public static async Task<string> GetNameAsync(
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
