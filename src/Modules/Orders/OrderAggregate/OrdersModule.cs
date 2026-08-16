namespace ALKAROS.Orders.OrderAggregate;

using ALKAROS.ModuleComposition;

public sealed class OrdersModule : IModule
{
    public string Id => "Orders";

    public string DisplayName => "Orders";

    public IReadOnlyCollection<string> DependsOn => Array.Empty<string>();

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IOrderRepository, PostgresOrderRepository>();
    }
}