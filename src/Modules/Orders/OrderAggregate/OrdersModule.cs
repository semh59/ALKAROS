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
    }
}