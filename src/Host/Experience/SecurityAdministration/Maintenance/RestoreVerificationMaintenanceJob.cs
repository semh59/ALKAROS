using ALKAROS.Operations.OffsiteBackup;
using ALKAROS.Operations.RestoreVerification;
using ALKAROS.Security.SecretRotation;
using Microsoft.Extensions.DependencyInjection;

namespace ALKAROS.Host.Experience.SecurityAdministration.Maintenance;

/// <summary>
/// V1-RMD-271: the restore drill. Takes the newest off-site artifact, decrypts and
/// verifies it, restores it into a throwaway database, runs the integrity and smoke
/// checks, measures the whole thing against the approved RTO and records the attempt.
/// An untested backup is a hope, not a backup: this is what proves the off-site copies
/// can actually be restored.
///
/// It exercises the artifacts the off-site job ships (<c>pg_dump</c> custom format,
/// filed as <see cref="DataClass.Settings"/>). The scratch database lives on the same
/// server as the application unless <c>ALKAROS_RESTORE_MAINTENANCE_CONNECTION</c>
/// points the drill elsewhere; restoring a large dump is real load, hence the daily
/// interval and the manual trigger for quiet hours.
/// </summary>
public sealed class RestoreVerificationMaintenanceJob : IMaintenanceJob
{
    private readonly ISecretRotationStore _rotationStore;

    public RestoreVerificationMaintenanceJob(ISecretRotationStore rotationStore)
    {
        _rotationStore = rotationStore ?? throw new ArgumentNullException(nameof(rotationStore));
    }

    public string Name => "restore-verification";

    public string Description => "En yeni uzak yedeği yalıtılmış geçici bir veritabanına geri yükleyip doğrular.";

    public TimeSpan Interval => TimeSpan.FromHours(24);

    public string? DisabledReason
        => _rotationStore.Find(OffsiteBackupMaintenanceJob.SecretName)?.ActiveVersion is null
            ? "Yedek şifreleme anahtarı henüz başlatılmamış; uzak yedekler çözülemez."
            : null;

    public async Task<MaintenanceJobOutcome> ExecuteAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scopedServices);
        var orchestrator = scopedServices.GetRequiredService<RestoreVerificationOrchestrator>();

        try
        {
            var attempt = await orchestrator.RunAsync(DataClass.Settings, cancellationToken);
            var rto = attempt.WithinRtoTarget ? "RTO hedefi içinde" : "RTO hedefi AŞILDI";
            return new MaintenanceJobOutcome(
                attempt.Succeeded ? MaintenanceJobStatusKind.Succeeded : MaintenanceJobStatusKind.Failed,
                $"{attempt.ArtifactId} geri yüklendi: {attempt.IntegrityChecksPassed}/{attempt.IntegrityChecksTotal} kontrol geçti, " +
                $"{attempt.Duration.TotalSeconds:0.#} sn, {rto}.");
        }
        catch (RestoreArtifactNotFoundException)
        {
            return new MaintenanceJobOutcome(
                MaintenanceJobStatusKind.Skipped,
                "Doğrulanacak uzak yedek yok; önce 'offsite-backup' işi bir yedek yüklemeli.");
        }
    }
}
