namespace ALKAROS.Settings.ReservationStation;

using ALKAROS.Settings.TypedSettings;

/// <summary>
/// V1-SET-003: the per-deployment switch for the dedicated Reservation
/// Station screen (a lean `/reservations` PosTerminal route showing only
/// the floor plan's reservation actions — Semih, 2026-09-04). Mirrors
/// <c>KitchenLiveSyncSetting</c>'s pattern exactly: reservation intake
/// doesn't look the same at every deployment either — a small neighborhood
/// restaurant routes it through the cashier's own screen (the existing
/// permission-gated reservation action already does that, unconditionally,
/// with no code change needed here), while a business with dedicated
/// reservation staff can turn this on to hand them their own screen instead.
/// Default off: the toggle exists so the app can tell an operator "this
/// deployment has a dedicated reservation station" — the underlying
/// tables.reserve permission model works identically either way.
/// </summary>
public static class ReservationStationSetting
{
    public const string Key = "reservations.dedicated_station_enabled";
    private const string ModuleOwner = "tables";

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
                        "Marks this deployment as having a dedicated reservation station " +
                        "(a lean /reservations screen for reservation-only staff). Off by " +
                        "default: without it, reservation intake stays on the cashier's own " +
                        "floor-plan screen exactly as before — this only changes whether the " +
                        "dedicated screen is offered, not the underlying tables.reserve " +
                        "permission model."),
                cancellationToken);
        }
        catch (DuplicateSettingKeyException)
        {
            // Already registered — an earlier boot of this same process, or
            // a previous deployment of this database. Leave it as-is; a
            // deployment's chosen value must never be silently reset.
        }
    }

    /// <summary>
    /// Whether this deployment has a dedicated reservation station. Registers
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
