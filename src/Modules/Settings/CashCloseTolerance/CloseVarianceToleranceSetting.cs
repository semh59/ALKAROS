namespace ALKAROS.Settings.CashCloseTolerance;

using ALKAROS.Settings.TypedSettings;

/// <summary>
/// The difference, in whole lira, a cash session may close with before a manager must override. The
/// default is 50 (what the code used to hardcode); the manager may change it, but never above <see cref="MaxValue"/>:
/// the ceiling is enforced when the value is written (<c>SettingValidator</c>) and again when it is read, so a value
/// that reached the table another way never widens the tolerance. Every change is audited by the settings trigger.
/// </summary>
public static class CloseVarianceToleranceSetting
{
    public const string Key = "cash.close_variance_tolerance";
    public const int MaxValue = 500;
    private const string ModuleOwner = "cash";
    private const int DefaultValue = 50;

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
                        "The cash-session closing difference (TRY, whole lira) above which a manager override is needed. " +
                        "At most 500."),
                cancellationToken);
        }
        catch (DuplicateSettingKeyException)
        {
            // Already registered; a deployment's chosen value must never be silently reset.
        }
    }

    /// <summary>This deployment's tolerance, never above the ceiling; the default until the setting is registered.</summary>
    public static async Task<int> GetToleranceAsync(
        ISettingsService settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var value = await settings.GetValueOrDefaultAsync(Key, DefaultValue, cancellationToken);
        return Math.Clamp(value, 0, MaxValue);
    }
}
