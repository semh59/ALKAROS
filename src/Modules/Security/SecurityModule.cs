namespace ALKAROS.Security;

using ALKAROS.ModuleComposition;
using ALKAROS.Secrets;
using ALKAROS.Security.DataProtectionRetention;
using ALKAROS.Security.IdentityHardening;
using ALKAROS.Security.SecretRotation;

public sealed class SecurityModule : IModule
{
    public string Id => "Security";
    public string DisplayName => "Security Hardening (Secrets, Identity, Data Protection)";
    public IReadOnlyCollection<string> DependsOn => ["Identity", "Audit"];

    public void Register(ModuleContext context)
    {
        // V15-SEC-001: file-based rotation-state metadata (never a raw
        // secret value, V0-ARC-005) under deployment/secrets/**, same
        // "outside the repo, deployment-owned" model the raw values use.
        // Overridable so a container/host can point it at its own mounted
        // volume; falls back to a path relative to the content root.
        context.RegisterSingleton<ISecretRotationStore>(_ =>
        {
            var rotationDirectory = Environment.GetEnvironmentVariable("ALKAROS_SECRET_ROTATION_DIR")
                ?? Path.Combine(AppContext.BaseDirectory, "deployment", "secrets", "rotation");
            return new FileSecretRotationStore(rotationDirectory);
        });
        context.RegisterTransient<IRotatingSecretResolver, RotatingSecretResolver>();

        // V15-SEC-002: identity hardening composes with Identity's own
        // AuthenticationService/IUserStore/IDeviceSessionService instead of
        // duplicating their persistence.
        // InMemorySuspiciousLoginAuditSink is a real, order-preserving
        // reference sink — a durable Postgres-backed one is a later
        // Host-wiring concern (no migration in this task's Owned surface).
        context.RegisterSingleton<ISuspiciousLoginAuditSink, InMemorySuspiciousLoginAuditSink>();
        context.RegisterTransient<SessionRotationService, SessionRotationService>();
        context.RegisterTransient<AccountRecoveryService, AccountRecoveryService>();
        context.RegisterTransient<SuspiciousLoginAuditingAuthenticationService, SuspiciousLoginAuditingAuthenticationService>();

        // V15-SEC-003: data-protection retention persists directly to
        // security.retention_subjects (migration 137); re-encryption
        // protects/unprotects through its own private RetentionAccessPolicy
        // chain (ISensitiveDataAccessPolicy has no shared DI registration by
        // design), keyed from the environment like every other envelope
        // consumer.
        context.RegisterTransient<IRetentionSubjectStore, PostgresRetentionSubjectStore>();
        context.RegisterTransient<ISecretProvider, EnvironmentVariableSecretProvider>();
        context.RegisterTransient<RetentionExecutionService, RetentionExecutionService>();
        context.RegisterTransient<DeletionQueueProcessor, DeletionQueueProcessor>();
        context.RegisterTransient<AuthorizedReEncryptionService, AuthorizedReEncryptionService>();
    }
}
