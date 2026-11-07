using ALKAROS.CustomerAccounts.BalanceProjection;
using ALKAROS.Host.Experience.SecurityAdministration.Maintenance;
using ALKAROS.Reporting.BusinessDayTotals;
using Microsoft.Extensions.DependencyInjection;

namespace ALKAROS.Host.Experience.CustomerAging;

/// <summary>
/// Writes the closing balance of every finished business day that has no snapshot yet. The ledger is immutable, so a
/// day computed late is exactly what it would have been on time; that is why the first run also fills the 90-day
/// lookback of the aging report. It runs on the shared maintenance timer and from the Security screen.
/// </summary>
public sealed class BalanceSnapshotMaintenanceJob : IMaintenanceJob
{
    /// <summary>Wider than the oldest lookback of the report (90 days) so a day late never leaves a gap.</summary>
    public const int WindowDays = 100;

    /// <summary>
    /// Daily balances are customer data derived from the ledger and are not invoice records, so they are kept for two
    /// years.
    /// </summary>
    public const int RetentionDays = 730;

    private readonly TimeProvider _clock;

    public BalanceSnapshotMaintenanceJob(TimeProvider clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public string Name => "balance-snapshot";

    public string Description => "Cari hesapların gün sonu bakiyelerini kaydeder (yaşlandırma raporundaki geçmiş bakiyeler buradan gelir) ve 2 yıldan eski kayıtları siler.";

    public TimeSpan Interval => TimeSpan.FromHours(6);

    public string? DisabledReason => null;

    public async Task<MaintenanceJobOutcome> ExecuteAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scopedServices);
        var store = scopedServices.GetRequiredService<IBalanceSnapshotStore>();

        var today = ReceivablesAgingEndpoints.Calendar.DateOf(_clock.GetUtcNow());
        var lastClosedDay = today.AddDays(-1);
        var firstDay = lastClosedDay.AddDays(1 - WindowDays);
        var done = await store.GetSnapshotDatesAsync(firstDay, cancellationToken);

        var written = 0;
        var days = 0;
        for (var day = firstDay; day <= lastClosedDay; day = day.AddDays(1))
        {
            if (done.Contains(day))
                continue;
            var count = await store.TakeClosingSnapshotsAsync(day, BusinessDayWindow.Of(day).End, cancellationToken);
            written += count;
            if (count > 0)
                days++;
        }

        var deleted = await store.DeleteOlderThanAsync(today.AddDays(-RetentionDays), cancellationToken);

        return new MaintenanceJobOutcome(
            MaintenanceJobStatusKind.Succeeded, $"{days} gün için {written} bakiye kaydı yazıldı; {deleted} eski kayıt silindi.");
    }
}
