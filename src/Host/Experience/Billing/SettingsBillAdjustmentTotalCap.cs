using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Identity.Authorization.PersonalBudgets;
using ALKAROS.Settings.BillAdjustmentCap;
using ALKAROS.Settings.TypedSettings;
using Microsoft.Extensions.DependencyInjection;

namespace ALKAROS.Host.Experience.Billing;

/// <summary>
/// Reads the per-bill comp and discount cap from the settings. The default is the personal comp budget's daily ceiling,
/// the one total the venue already runs on, so the cap needs no number of its own. The settings service is resolved per
/// read because the settings management host registers it scoped while this cap is held by a singleton gate.
/// </summary>
public sealed class SettingsBillAdjustmentTotalCap : IBillAdjustmentTotalCap
{
    public const int DefaultCap = (int)PersonalCompBudgetPolicy.DailyCap;

    private readonly IServiceScopeFactory _scopes;

    public SettingsBillAdjustmentTotalCap(IServiceScopeFactory scopes)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
    }

    public async Task<decimal> GetAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopes.CreateScope();
        return await BillAdjustmentTotalCapSetting.GetCapAsync(
            scope.ServiceProvider.GetRequiredService<ISettingsService>(), DefaultCap, cancellationToken);
    }
}
