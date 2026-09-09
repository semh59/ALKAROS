namespace ALKAROS.Settings.QrOrderExpiry;

using ALKAROS.Settings.TypedSettings;

/// <summary>
/// V12-QRO-002. docs/design/modules/qr-nfc-ordering.md §5: an unconfirmed QR
/// order automatically cancels after this long, releasing the table it
/// holds as Reserved back to Available — the actual "an old/duplicate QR
/// order cannot lock a table forever" mechanism, without inventing a
/// separate table-level timer (table-reservation-policy.md: the table's
/// Reserved status has no expiry of its own, it always converges to its
/// owning order's state). Default 5 minutes — the design doc's own
/// rationale: 15 was found too long; normal service rhythm already brings
/// staff to the table within 5, and a suspicious order should not stay
/// "live" in the system for long. Per-deployment adjustable, same pattern
/// as kitchen.live_sync_enabled (V1-SET-002).
/// </summary>
public static class QrOrderExpirySetting
{
    public const string Key = "qr_ordering.pending_confirmation_timeout";
    private const string ModuleOwner = "qr_ordering";
    public static readonly TimeSpan Default = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Registers the setting (value <see cref="Default"/>) if it does not
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
                    Default.ToString(),
                    SettingDataType.Duration,
                    SettingScope.Global,
                    ModuleOwner,
                    Description:
                        "How long an unconfirmed QR order may stay PendingConfirmation before it " +
                        "auto-cancels and releases the table it holds as Reserved."),
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
    /// This deployment's confirmation timeout. Registers the setting (at
    /// its default) the first time it is asked, so no separate startup hook
    /// is needed.
    /// </summary>
    public static async Task<TimeSpan> GetTimeoutAsync(
        ISettingsService settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var record = await settings.GetRecordAsync(Key, cancellationToken);
        if (record is null)
        {
            await EnsureRegisteredAsync(settings, cancellationToken);
            return Default;
        }

        return await settings.GetValueOrDefaultAsync(Key, Default, cancellationToken);
    }
}
