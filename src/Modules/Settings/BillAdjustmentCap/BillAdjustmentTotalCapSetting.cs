namespace ALKAROS.Settings.BillAdjustmentCap;

using ALKAROS.Settings.TypedSettings;

/// <summary>
/// The most, in whole lira, that comp and discount grants may take off one bill before a manager has to approve the
/// next one. The caller supplies the default, so the cap follows the comp budget the venue already runs on instead of
/// a second number; the manager may change it, never above <see cref="MaxValue"/>.
/// </summary>
public static class BillAdjustmentTotalCapSetting
{
    public const string Key = "billing.adjustment_total_cap";
    public const int MaxValue = 100_000;
    private const string ModuleOwner = "billing";

    public static async Task EnsureRegisteredAsync(
        ISettingsService settings, int defaultValue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            await settings.RegisterSettingAsync(
                new RegisterSettingRequest(
                    Key,
                    defaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    SettingDataType.WholeNumber,
                    SettingScope.Global,
                    ModuleOwner,
                    Description:
                        "The total (TRY, whole lira) of comp and discount grants one bill may carry before a manager must " +
                        "approve the next one. 0 means every comp or discount of a cashier or waiter needs approval."),
                cancellationToken);
        }
        catch (DuplicateSettingKeyException)
        {
            // Already registered; a deployment's chosen value must never be silently reset.
        }
    }

    /// <summary>This deployment's cap, never above the ceiling; <paramref name="defaultValue"/> until the setting is registered.</summary>
    public static async Task<int> GetCapAsync(
        ISettingsService settings, int defaultValue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var record = await settings.GetRecordAsync(Key, cancellationToken);
        if (record is null)
        {
            await EnsureRegisteredAsync(settings, defaultValue, cancellationToken);
            return defaultValue;
        }

        return Math.Clamp(await settings.GetValueOrDefaultAsync(Key, defaultValue, cancellationToken), 0, MaxValue);
    }
}
