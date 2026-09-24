using ALKAROS.Security.DataProtectionRetention;
using Microsoft.Extensions.DependencyInjection;

namespace ALKAROS.Host.Experience.SecurityAdministration.Maintenance;

/// <summary>
/// V1-RMD-268: runs the V15-SEC-003 retention sweep (dispose expired subjects)
/// and then drains the deletion queue (purge what a sweep queued earlier).
/// Both steps are idempotent, so a manual run next to the daily timer is safe.
/// </summary>
public sealed class RetentionSweepMaintenanceJob : IMaintenanceJob
{
    public string Name => "retention-sweep";

    public string Description => "Saklama süresi dolan hassas kayıtları imha eder ve silme kuyruğunu boşaltır.";

    public TimeSpan Interval => TimeSpan.FromHours(24);

    public string? DisabledReason => null;

    public async Task<MaintenanceJobOutcome> ExecuteAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scopedServices);
        var correlationId = $"retention-sweep:{Guid.NewGuid():N}";

        var sweep = await scopedServices.GetRequiredService<RetentionExecutionService>()
            .RunSweepAsync(DateTimeOffset.UtcNow, correlationId, cancellationToken);
        var purged = await scopedServices.GetRequiredService<DeletionQueueProcessor>()
            .ProcessAsync(correlationId, cancellationToken);

        return new MaintenanceJobOutcome(
            MaintenanceJobStatusKind.Succeeded,
            $"{sweep.Disposed.Count} kayıt imha edildi, {purged.Count} kayıt kalıcı silindi; " +
            $"{sweep.SkippedLegalHold.Count} yasal tutuklu, {sweep.SkippedRetain.Count} saklanan, " +
            $"{sweep.SkippedNotExpired.Count} süresi dolmamış kayıt atlandı.");
    }
}
