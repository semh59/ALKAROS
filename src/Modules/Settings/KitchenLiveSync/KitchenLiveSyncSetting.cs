namespace ALKAROS.Settings.KitchenLiveSync;

using ALKAROS.Settings.TypedSettings;

/// <summary>
/// V1-SET-002: the single per-deployment switch behind the whole
/// kitchen-order sync / waiter ready-notification / sent-but-unserved void
/// feature cluster (docs/domain/void-complimentary-discount-policy.md
/// Amendment, 2026-09-04). Default off — kitchens differ too much between
/// a steam-table canteen (items are effectively always "prepared" already),
/// a fast-food line and a fine-dining kitchen for one hard-coded timing
/// model to fit every deployment; a restaurant only gets the live-sync
/// behaviour if it turns this on.
/// </summary>
public static class KitchenLiveSyncSetting
{
    public const string Key = "kitchen.live_sync_enabled";
    private const string ModuleOwner = "kitchen";

    /// <summary>
    /// Registers the setting (value <c>false</c>) if it does not already
    /// exist. Safe to call repeatedly — a second registration is a no-op,
    /// not an error.
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
                    "false",
                    SettingDataType.Toggle,
                    SettingScope.Global,
                    ModuleOwner,
                    Description:
                        "Sends kitchen ticket item state (preparing/ready/served) back to the " +
                        "order it came from, enabling the waiter ready-notification and the " +
                        "sent-but-unserved void path. Off by default: a deployment turns it on " +
                        "only if its kitchen workflow benefits from live status tracking."),
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
    /// Whether this deployment has turned live kitchen sync on. Registers
    /// the setting (at its default, off) the first time it is asked, so no
    /// separate startup hook is needed.
    /// </summary>
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
