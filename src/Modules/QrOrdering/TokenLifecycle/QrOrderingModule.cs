using ALKAROS.ModuleComposition;
using ALKAROS.QrOrdering.CustomerSession;
using ALKAROS.QrOrdering.RelayCredential;
using ALKAROS.QrOrdering.RelaySecurity;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;

namespace ALKAROS.QrOrdering.TokenLifecycle;

public sealed class QrOrderingModule : IModule
{
    public string Id => "QrOrdering";

    public string DisplayName => "QR Ordering";

    // Table binding is a read of table_mgmt.tables' existence, not a write —
    // no direct-call edge is declared (V0-ARC-001 row 1: reads across
    // schemas are always allowed without a dependency declaration).
    public IReadOnlyCollection<string> DependsOn => [];

    public void Register(ModuleContext context)
        => context
            .RegisterTransient<ITableTokenRepository, PostgresTableTokenRepository>()
            .RegisterTransient<TableTokenService, TableTokenService>()
            // V12-QRT-003: this module is the first real consumer of the
            // Secrets/SensitiveData building blocks — registered here rather
            // than at Host startup because nothing else uses them yet; a
            // later second consumer would promote these to a shared
            // registration point instead of duplicating them.
            .RegisterTransient<ISecretProvider, EnvironmentVariableSecretProvider>()
            .RegisterTransient<ISecretAccessPolicy, RelayCredentialAccessPolicy>()
            .RegisterTransient<ISensitiveDataAccessPolicy, RelayCredentialAccessPolicy>()
            .RegisterTransient<ISecretResolver, SecretResolver>()
            .RegisterTransient<IEnvelopeCipher, AesGcmEnvelopeCipher>()
            .RegisterTransient<SensitivePayloadProtector, SensitivePayloadProtector>()
            .RegisterTransient<IRelayCredentialStore, PostgresRelayCredentialStore>()
            .RegisterTransient<IRelayNonceStore, PostgresRelayNonceStore>()
            .RegisterTransient<RelayRequestValidator, RelayRequestValidator>()
            // V12-QRS-003.
            .RegisterTransient<ICustomerSessionRepository, PostgresCustomerSessionRepository>()
            .RegisterTransient<CustomerSessionService, CustomerSessionService>();
}
