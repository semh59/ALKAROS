namespace ALKAROS.Operations;

using ALKAROS.ModuleComposition;
using ALKAROS.Observability.StructuredLogging;
using ALKAROS.Operations.BackupHealth;
using ALKAROS.Operations.OffsiteBackup;
using ALKAROS.Operations.RestoreVerification;
using ALKAROS.Secrets;
using ALKAROS.Security.SecretRotation;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

public sealed class OperationsModule : IModule
{
    public string Id => "Operations";
    public string DisplayName => "System Operations and Backup";
    public IReadOnlyCollection<string> DependsOn => ["Observability", "Security"];

    // V15-BKP-001: the logical secret name this deployment's off-site
    // backups are encrypted under (versioned through V15-SEC-001's
    // rotation state, never the raw value itself).
    private const string OffsiteBackupSecretName = "offsite-backup";

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IBackupHealthRepository, PostgresBackupHealthRepository>();
        context.RegisterTransient<IBackupEngine, LocalBackupEngine>();
        context.RegisterTransient<IBackupHealthService, BackupHealthService>();

        // V15-BKP-001. IOffsiteBackupTarget: a real off-site destination
        // (S3-compatible bucket, etc.) is a deployment-specific choice this
        // task's Owned surface does not make (provider-agnostic
        // IOffsiteBackupTarget was the point) — a local directory is
        // registered as the default so the upload/checksum/retry/receipt
        // chain is live end-to-end; pointing ALKAROS_OFFSITE_BACKUP_DIR at
        // a real mounted off-site volume/share makes it a genuine off-site
        // copy without any code change.
        context.RegisterSingleton<IOffsiteBackupTarget>(_ =>
        {
            var directory = Environment.GetEnvironmentVariable("ALKAROS_OFFSITE_BACKUP_DIR")
                ?? Path.Combine(AppContext.BaseDirectory, "deployment", "offsite-backup");
            return new LocalDirectoryOffsiteBackupTarget(directory);
        });
        context.RegisterTransient<IOffsiteBackupReceiptStore, PostgresOffsiteBackupReceiptStore>();
        context.RegisterSingleton<OffsiteBackupCipher>(sp => new OffsiteBackupCipher(
            OffsiteBackupSecretName,
            sp.GetRequiredService<ISecretRotationStore>(),
            sp.GetRequiredService<ISecretProvider>()));
        context.RegisterTransient<OffsiteBackupUploadService, OffsiteBackupUploadService>();
        context.RegisterTransient<OffsiteBackupRestoreVerificationService, OffsiteBackupRestoreVerificationService>();

        // V15-BKP-002. The isolated-restore-verification maintenance
        // connection defaults to the same Postgres server the application
        // itself already connects to (a sibling scratch database, created
        // and dropped per attempt) — the natural default for "restore into
        // an isolated instance" when no separate maintenance target is
        // configured. ALKAROS_RESTORE_MAINTENANCE_CONNECTION overrides it
        // for a deployment that points restore drills at a dedicated
        // instance instead.
        context.RegisterTransient<IRestoreAttemptStore, PostgresRestoreAttemptStore>();
        context.RegisterSingleton<IIsolatedRestoreDatabaseFactory>(sp =>
        {
            var connectionString = Environment.GetEnvironmentVariable("ALKAROS_RESTORE_MAINTENANCE_CONNECTION")
                ?? sp.GetRequiredService<NpgsqlDataSource>().ConnectionString;
            return new NpgsqlIsolatedRestoreDatabaseFactory(connectionString);
        });
        // A minimal, honest default: every restored database is expected to
        // still have its foundational reference tables and be queryable at
        // all. A deployment that wants deeper per-table checks extends this
        // list without touching the orchestrator itself.
        context.RegisterSingleton<IReadOnlyList<IntegrityCheck>>(_ => new IntegrityCheck[]
        {
            new(
                "identity.users is queryable",
                "SELECT count(*) FROM identity.users;",
                value => value is not null),
            new(
                "table_mgmt.tables is queryable",
                "SELECT count(*) FROM table_mgmt.tables;",
                value => value is not null),
        });
        context.RegisterTransient<RestoreVerificationOrchestrator, RestoreVerificationOrchestrator>();
    }
}
