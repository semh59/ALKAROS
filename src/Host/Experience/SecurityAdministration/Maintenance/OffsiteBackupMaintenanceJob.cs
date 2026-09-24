using ALKAROS.Operations.OffsiteBackup;
using ALKAROS.Security.SecretRotation;
using Microsoft.Extensions.DependencyInjection;

namespace ALKAROS.Host.Experience.SecurityAdministration.Maintenance;

/// <summary>
/// V1-RMD-270: ships the local <c>backup.sh</c> artifacts off-site. Each
/// <c>*.dump</c> that has its SHA-256 sidecar and is not yet in the off-site
/// target is verified against that checksum, encrypted client-side under the
/// active version of the <c>offsite-backup</c> secret, uploaded, and recorded as
/// a receipt. Nothing is uploaded twice (the target is immutable and the job
/// checks first); one artifact failing never stops the others.
///
/// A <c>pg_dump</c> is a point-in-time snapshot, so it is filed under
/// <see cref="DataClass.Settings"/> (24 h RPO) only. The tighter Fiscal (5 min)
/// and OrdersInventory (1 h) targets need continuous WAL shipping, which this job
/// does NOT do; the RPO report (<c>GET .../backup/rpo</c>) shows that gap honestly.
/// </summary>
public sealed class OffsiteBackupMaintenanceJob : IMaintenanceJob
{
    public const string SecretName = "offsite-backup";
    public const string SourceDirectoryVariable = "ALKAROS_BACKUP_DIR";
    public const int MaxArtifactsPerRun = 10;
    public const long MaxArtifactBytes = 1L * 1024 * 1024 * 1024;

    private readonly ISecretRotationStore _rotationStore;

    public OffsiteBackupMaintenanceJob(ISecretRotationStore rotationStore)
    {
        _rotationStore = rotationStore ?? throw new ArgumentNullException(nameof(rotationStore));
    }

    public string Name => "offsite-backup";

    public string Description => "Yerel yedek dosyalarını doğrular, şifreler ve uzak hedefe yükler.";

    public TimeSpan Interval => TimeSpan.FromHours(1);

    public string? DisabledReason
    {
        get
        {
            var directory = Environment.GetEnvironmentVariable(SourceDirectoryVariable);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return $"{SourceDirectoryVariable} ayarlanmamış ya da dizin yok; yerel yedek kaynağı bilinmiyor.";
            if (_rotationStore.Find(SecretName)?.ActiveVersion is null)
                return "Yedek şifreleme anahtarı henüz başlatılmamış; önce 'offsite-backup' gizli anahtarının rotasyonunu başlatın.";
            return null;
        }
    }

    public async Task<MaintenanceJobOutcome> ExecuteAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scopedServices);
        var directory = Environment.GetEnvironmentVariable(SourceDirectoryVariable)!;
        var target = scopedServices.GetRequiredService<IOffsiteBackupTarget>();
        var uploader = scopedServices.GetRequiredService<OffsiteBackupUploadService>();
        var receipts = scopedServices.GetRequiredService<IOffsiteBackupReceiptStore>();

        var uploaded = 0;
        var alreadyThere = 0;
        var failures = new List<string>();
        var candidates = Directory.EnumerateFiles(directory, "*.dump")
            .Where(path => File.Exists(path + ".sha256"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        foreach (var path in candidates)
        {
            var artifactId = Path.GetFileName(path);
            if (await target.ExistsAsync(artifactId, cancellationToken))
            {
                alreadyThere++;
                continue;
            }

            if (uploaded + failures.Count >= MaxArtifactsPerRun)
                break;

            try
            {
                var info = new FileInfo(path);
                if (info.Length > MaxArtifactBytes)
                {
                    failures.Add($"{artifactId} (boyut sınırını aşıyor)");
                    continue;
                }

                var checksum = await ReadSidecarChecksumAsync(path + ".sha256", cancellationToken);
                var receipt = await uploader.UploadAsync(
                    new BackupArtifactReference(artifactId, DataClass.Settings, path, checksum, info.LastWriteTimeUtc),
                    cancellationToken);
                await receipts.RecordAsync(receipt, cancellationToken);
                uploaded++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Only the artifact name and the failure type are reported; the message may carry paths or secrets.
                failures.Add($"{artifactId} ({exception.GetType().Name})");
            }
        }

        var summary = $"{uploaded} yedek yüklendi, {alreadyThere} yedek zaten uzakta.";
        return failures.Count == 0
            ? new MaintenanceJobOutcome(MaintenanceJobStatusKind.Succeeded, summary)
            : new MaintenanceJobOutcome(
                MaintenanceJobStatusKind.Failed,
                $"{summary} Başarısız: {string.Join(", ", failures)}.");
    }

    /// <summary>Reads the first token of a <c>sha256sum</c>-format sidecar ("hash  filename").</summary>
    private static async Task<string> ReadSidecarChecksumAsync(string sidecarPath, CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(sidecarPath, cancellationToken);
        var token = text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (token is null || token.Length != 64)
            throw new InvalidDataException("Checksum sidecar is not in sha256sum format.");
        return token.ToLowerInvariant();
    }
}
