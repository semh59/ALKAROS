namespace ALKAROS.Orders.OrderAggregate;

using ALKAROS.IntegrationContracts;
using ALKAROS.ModuleComposition;
using ALKAROS.Orders.Integration;

public sealed class OrdersModule : IModule
{
    public string Id => "Orders";

    public string DisplayName => "Orders";

    public IReadOnlyCollection<string> DependsOn => Array.Empty<string>();

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IOrderRepository, PostgresOrderRepository>();

        // Reacts to Table Management merge/transfer/unmerge events by moving
        // Order's own rows to the new table (V0-ARC-001 row 3).
        context.RegisterTransient<IIntegrationEventConsumer, TableEventOrderConsumer>();

        // V1-KIT-005: mirrors a kitchen ticket item's state onto its order
        // item (only published when a deployment turns on kitchen live-sync,
        // V1-SET-002).
        context.RegisterTransient<IIntegrationEventConsumer, KitchenEventOrderConsumer>();

        // V12-QRO-001: materializes the actual Order from a QR Ordering
        // submission (V0-ARC-001 row 19 — QR Ordering has no direct-call
        // edge to Order, only an integration event). SubmitOrderHandler
        // itself is registered by Host's AddOrderManagementExperience
        // (src/Host/Experience/Orders/OrderManagementEndpoints.cs), not
        // here — same TryAddSingleton the DualScreen quick-sale route and
        // NFC ordering already share.
        context.RegisterTransient<IIntegrationEventConsumer, QrOrderSubmittedConsumer>();
    }
}