namespace ALKAROS.Settings.CashMovementApproval;

using ALKAROS.Settings.TypedSettings;

/// <summary>
/// the cash-out amount, in whole lira, above which a manager must approve the payout.
/// An amount equal to the threshold needs no approval; 0 means every cash-out needs one. The default is 500: daily
/// small expenses (water, bread, courier) go out freely while a supplier payment or an advance is approved.
/// </summary>
public static class CashOutApprovalThresholdSetting
{
    public const string Key = "cash.cash_out_approval_threshold";
    public const int MaxValue = 1_000_000;
    private const string ModuleOwner = "cash";
    private const int DefaultValue = 500;

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
                        "The cash-out amount (TRY, whole lira) above which a manager must approve the payout. " +
                        "0 means every cash-out needs approval."),
                cancellationToken);
        }
        catch (DuplicateSettingKeyException)
        {
            // Already registered; a deployment's chosen value must never be silently reset.
        }
    }

    /// <summary>This deployment's threshold; registers the setting at its default the first time it is asked.</summary>
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
