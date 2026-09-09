using ALKAROS.ModuleComposition;
using ALKAROS.QrOrdering.CustomerSession;
using ALKAROS.QrOrdering.PendingOrders;
using ALKAROS.QrOrdering.RelayCredential;
using ALKAROS.QrOrdering.RelaySecurity;
using ALKAROS.QrOrdering.TablePolicy;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;

namespace ALKAROS.QrOrdering.TokenLifecycle;

public sealed class QrOrderingModule : IModule
{
    public string Id => "QrOrdering";

    public string DisplayName => "QR Ordering";

    // V12-QRO-002: module-dependency-rules.md row 19 approves QR Ordering ->
    // Table Management (same-transaction table reservation on submission) —
    // approved 2026-08-03, exercised in code for the first time here.
    public IReadOnlyCollection<string> DependsOn => ["Tables"];

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
            .RegisterTransient<CustomerSessionService, CustomerSessionService>()
            // V12-QRO-002: ITableRepository itself is not registered here —
            // it is Table Management's own service (already registered by
            // TablesModule; the DependsOn edge above guarantees that module
            // is present in the same composition), consumed directly through
            // the approved edge, same pattern as Production consuming
            // Inventory's IStockBalanceRepository without re-registering it.
            .RegisterTransient<QrTableReservationPolicy, QrTableReservationPolicy>()
            // V12-QRO-001.
            .RegisterTransient<QrPendingOrderStore, QrPendingOrderStore>();
}
