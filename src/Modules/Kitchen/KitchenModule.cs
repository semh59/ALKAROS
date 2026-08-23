namespace ALKAROS.Kitchen;

using ALKAROS.Kitchen.PhysicalPrintRecovery;
using ALKAROS.Kitchen.PrintQueue;
using ALKAROS.Kitchen.Routing;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.ModuleComposition;

public sealed class KitchenModule : IModule
{
    public string Id => "Kitchen";
    public string DisplayName => "Kitchen and Print Operations";
    public IReadOnlyCollection<string> DependsOn => [];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IKitchenTicketRepository, PostgresKitchenTicketRepository>();
        context.RegisterTransient<IPrinterRepository, PostgresPrinterRepository>();
        context.RegisterTransient<IPrinterRouteRepository, PostgresPrinterRouteRepository>();
        context.RegisterTransient<IKitchenPrinterRouter, KitchenPrinterRouter>();
        context.RegisterTransient<IKitchenRoutingService, KitchenRoutingService>();
        context.RegisterTransient<IPrintQueueRepository, PostgresPrintQueueRepository>();
        context.RegisterTransient<IPrintQueueService, PrintQueueService>();
        context.RegisterTransient<IPhysicalPrintRecoveryRepository, PostgresPhysicalPrintRecoveryRepository>();
        context.RegisterTransient<IPhysicalPrintRecoveryService, PhysicalPrintRecoveryService>();
    }
}
