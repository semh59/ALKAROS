using ALKAROS.CustomerAccounts.BalanceProjection;
using ALKAROS.Host.Experience.SecurityAdministration.Maintenance;
using Microsoft.Extensions.DependencyInjection;

namespace ALKAROS.Host.Experience.CustomerAging;

/// <summary>
/// The drift detector and repair path of the cached customer balance: the ledger is the source of truth, so a cached
/// balance that disagrees with it is rebuilt from the ledger.
/// </summary>
public sealed class BalanceReconcileMaintenanceJob : IMaintenanceJob
{
    /// <summary>Bounds one run; anything beyond it is repaired by the next run.</summary>
    public const int MaxRepairsPerRun = 500;

    public string Name => "balance-reconcile";

    public string Description => "Önbellekteki cari bakiyeleri defterle karşılaştırır; uyumsuz olanları defterden yeniden hesaplar.";

    public TimeSpan Interval => TimeSpan.FromHours(24);

    public string? DisabledReason => null;

    public async Task<MaintenanceJobOutcome> ExecuteAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scopedServices);
        var balances = scopedServices.GetRequiredService<IAccountBalanceProjection>();

        var drifted = await balances.FindDriftedCustomersAsync(MaxRepairsPerRun, cancellationToken);
        foreach (var customerId in drifted)
            await balances.RebuildAsync(customerId, cancellationToken);

        return new MaintenanceJobOutcome(
            MaintenanceJobStatusKind.Succeeded,
            drifted.Count == 0
                ? "Tüm müşteri bakiyeleri defterle uyumlu."
                : $"{drifted.Count} müşterinin bakiyesi defterle uyumsuzdu ve yeniden hesaplandı.");
    }
}
