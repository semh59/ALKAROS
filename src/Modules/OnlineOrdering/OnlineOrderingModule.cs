using ALKAROS.IntegrationContracts;
using ALKAROS.ModuleComposition;
using ALKAROS.OnlineOrdering.AvailabilityPublishing;
using ALKAROS.OnlineOrdering.Credentials;
using ALKAROS.OnlineOrdering.Polling;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.OrderIntake;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.StatusSync;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.Menu;
using ALKAROS.Catalog.ProductCatalog;
using ALKAROS.OnlineOrdering.Providers.Inbox;
using ALKAROS.OnlineOrdering.StoreStatus;
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
        // V12-ONL-010: the shared inbox; it only needs the envelope master key.
        context.RegisterTransient(services => new ProviderInbox(
            (NpgsqlDataSource)services.GetService(typeof(NpgsqlDataSource))!,
            (ISecretProvider)services.GetService(typeof(ISecretProvider))!));
        // V12-ONL-009: polls every platform adapter that offers an order list into the same inbox.
        context.RegisterTransient(services => new OnlineOrderPoller(
            (NpgsqlDataSource)services.GetService(typeof(NpgsqlDataSource))!,
            (ProviderInbox)services.GetService(typeof(ProviderInbox))!,
            (IEnumerable<IOnlineOrderPollingSource>)services.GetService(typeof(IEnumerable<IOnlineOrderPollingSource>))!));
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

        // V12-TGO-002/003 (UNVERIFIED DRAFT): Trendyol Go events are stored by its webhook and by polling its package
        // list; the adapter turns them into orders and reports acceptance, readiness and cancellations back.
        context.RegisterTransient(services => new TrendyolGoWebhookInbox(
            (ProviderInbox)services.GetService(typeof(ProviderInbox))!, PlatformSettings(services)));
        context.RegisterTransient(services => new TrendyolGoOrderNormalizer(
            TrendyolGoMappings(services),
            (IProductRepository)services.GetService(typeof(IProductRepository))!,
            (ITaxProfileRepository)services.GetService(typeof(ITaxProfileRepository))!));
        context.RegisterSingleton<IOnlineOrderPollingSource>(services => new TrendyolGoOrderPollingSource(
            new HttpClient { Timeout = TimeSpan.FromSeconds(10) }, PlatformSettings(services), TimeProvider.System));
        context.RegisterTransient<IOnlineOrderProvider, TrendyolGoOnlineOrderProvider>();
        // One client instance, so its per-endpoint rate window is shared by every delivery.
        context.RegisterSingleton(services => new TrendyolGoStatusClient(
            new HttpClient { Timeout = TimeSpan.FromSeconds(10) }, PlatformSettings(services), TimeProvider.System));
        context.RegisterTransient<IIntegrationEventConsumer>(services => new TrendyolGoStatusUpdateConsumer(
            (TrendyolGoStatusClient)services.GetService(typeof(TrendyolGoStatusClient))!,
            (NpgsqlDataSource)services.GetService(typeof(NpgsqlDataSource))!,
            (ProviderInbox)services.GetService(typeof(ProviderInbox))!,
            PlatformSettings(services)));
        context.RegisterSingleton(TrendyolGoStatusSync.RetryProfile);
        // V12-TGO-004: Trendyol Go's catalog (price, on-sale status) and availability channels.
        context.RegisterSingleton(services => new TrendyolGoMenuClient(
            new HttpClient { Timeout = TimeSpan.FromSeconds(10) }, PlatformSettings(services), TimeProvider.System));
        context.RegisterTransient<ICatalogChannelPublisher>(services => new TrendyolGoCatalogPublisher(
            TrendyolGoMappings(services), (TrendyolGoMenuClient)services.GetService(typeof(TrendyolGoMenuClient))!, TimeProvider.System));
        context.RegisterTransient<IAvailabilityChannelPublisher>(services => new TrendyolGoAvailabilityPublisher(
            TrendyolGoMappings(services), (TrendyolGoMenuClient)services.GetService(typeof(TrendyolGoMenuClient))!));

        // V12-ONL-011 (UNVERIFIED DRAFT): each platform's open/closed switch and the service that delivers requests.
        context.RegisterTransient<IOnlineStoreStatusChannel>(services => new YemeksepetiStoreStatusChannel(
            (IYemeksepetiPartnerClient)services.GetService(typeof(IYemeksepetiPartnerClient))!, PlatformSettings(services)));
        context.RegisterSingleton<IOnlineStoreStatusChannel>(services => new TrendyolGoStoreStatusChannel(
            new HttpClient { Timeout = TimeSpan.FromSeconds(10) }, PlatformSettings(services)));
        context.RegisterTransient(services => new OnlineStoreStatusService(
            (NpgsqlDataSource)services.GetService(typeof(NpgsqlDataSource))!,
            (IEnumerable<IOnlineStoreStatusChannel>)services.GetService(typeof(IEnumerable<IOnlineStoreStatusChannel>))!,
            (TimeProvider)services.GetService(typeof(TimeProvider))!));
    }

    /// <summary>V12-TGO-002/004: the shared mapping service reading Trendyol Go's rows.</summary>
    private static PostgresYemeksepetiProductMappingService TrendyolGoMappings(IServiceProvider services) => new(
        (NpgsqlDataSource)services.GetService(typeof(NpgsqlDataSource))!,
        (IProductRepository)services.GetService(typeof(IProductRepository))!,
        (IProductModifierGroupRepository)services.GetService(typeof(IProductModifierGroupRepository))!,
        (IModifierGroupRepository)services.GetService(typeof(IModifierGroupRepository))!,
        TrendyolGoEvents.Provider);

    private static StoredOnlinePlatformSecretProvider PlatformSettings(IServiceProvider services) => new(
        (IOnlinePlatformCredentialStore)services.GetService(typeof(IOnlinePlatformCredentialStore))!,
        (ISecretProvider)services.GetService(typeof(ISecretProvider))!);
}
