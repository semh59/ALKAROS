using ALKAROS.IntegrationContracts;
using ALKAROS.ModuleComposition;
using ALKAROS.OnlineOrdering.AvailabilityPublishing;
using ALKAROS.OnlineOrdering.Credentials;
using ALKAROS.OnlineOrdering.Yemeksepeti.Provider;
using ALKAROS.OnlineOrdering.Providers.Contracts;
using ALKAROS.OnlineOrdering.AvailabilityPublishing.Yemeksepeti;
using ALKAROS.OnlineOrdering.CatalogPublishing;
using ALKAROS.OnlineOrdering.CatalogPublishing.Yemeksepeti;
using ALKAROS.OnlineOrdering.Yemeksepeti.OrderNormalization;
using ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;
using ALKAROS.OnlineOrdering.Yemeksepeti.WebhookInbox;
using ALKAROS.Secrets;
using Npgsql;

namespace ALKAROS.OnlineOrdering;

/// <summary>
/// Online ordering channels (module-dependency-rules.md row 20). Today the only channel is
/// Yemeksepeti, built against the public Partner API v2.0.2 document without sandbox
/// access (V0-YSP-001 is Blocked; V12-GOV-004 waiver).
/// </summary>
public sealed class OnlineOrderingModule : IModule
{
    public string Id => "OnlineOrdering";

    public string DisplayName => "Online Ordering";

    public IReadOnlyCollection<string> DependsOn => ["Catalog"];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IYemeksepetiProductMappingService, PostgresYemeksepetiProductMappingService>();
        // V12-ONL-001: the inbox builds its own resolver/cipher/protector chain around its
        // single-purpose access policy; only the secret source itself comes from DI.
        context.RegisterTransient<ISecretProvider, EnvironmentVariableSecretProvider>();
        // V12-OUI-003: platform settings a manager stored from the interface; the webhook secret and the partner
        // client's settings come from there first and from environment variables only when a field is not stored.
        context.RegisterTransient<IOnlinePlatformCredentialStore, PostgresOnlinePlatformCredentialStore>();
        context.RegisterTransient(services => new YemeksepetiWebhookInbox(
            (NpgsqlDataSource)services.GetService(typeof(NpgsqlDataSource))!,
            PlatformSettings(services)));
        // V12-ONL-002: payload normalization.
        context.RegisterTransient<YemeksepetiOrderNormalizer, YemeksepetiOrderNormalizer>();
        // V12-ONL-007: the platforms behind the shared online ordering contract.
        context.RegisterTransient<IOnlineOrderProvider, YemeksepetiOnlineOrderProvider>();
        context.RegisterTransient<OnlineOrderProviderRegistry, OnlineOrderProviderRegistry>();
        // V12-ONL-003: committed local status changes reach the provider through the outbox. One
        // client instance so its cached access token is shared (UNVERIFIED DRAFT provider calls).
        context.RegisterSingleton<IYemeksepetiPartnerClient>(services => new YemeksepetiPartnerHttpClient(
            new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
            PlatformSettings(services),
            TimeProvider.System));
        context.RegisterTransient<IIntegrationEventConsumer, YemeksepetiStatusUpdateConsumer>();
        // V12-RMD-008: a provider outage lasts minutes, not the default budget's ~15 s; these deliveries are tried
        // for about three hours before they are dead (and surface as a reconciliation case).
        context.RegisterSingleton(YemeksepetiStatusSync.RetryProfile);
        // V12-ONL-004: catalog publication per channel, delivered through the outbox.
        context.RegisterSingleton<TimeProvider>(TimeProvider.System);
        context.RegisterTransient<ICatalogChannelPublisher, YemeksepetiCatalogPublisher>();
        context.RegisterTransient<CatalogPublicationService, CatalogPublicationService>();
        context.RegisterTransient<IIntegrationEventConsumer, CatalogPublicationConsumer>();
        context.RegisterSingleton(YemeksepetiStatusSync.ProviderRetryProfile(CatalogPublicationService.RequestedEventType));
        // V12-ONL-005: availability publishing per enabled channel.
        context.RegisterTransient<IAvailabilityChannelPublisher, YemeksepetiAvailabilityPublisher>();
        context.RegisterTransient<AvailabilityPublicationService, AvailabilityPublicationService>();
    }

    private static StoredOnlinePlatformSecretProvider PlatformSettings(IServiceProvider services) => new(
        (IOnlinePlatformCredentialStore)services.GetService(typeof(IOnlinePlatformCredentialStore))!,
        (ISecretProvider)services.GetService(typeof(ISecretProvider))!);
}
