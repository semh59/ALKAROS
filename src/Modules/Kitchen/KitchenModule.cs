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
    // Kitchen consumes Orders' public contract (order items -> ticket) in the
    // order-submission transaction; the ALKAROS.Orders project reference makes
    // this a real compile dependency, so it is declared here (V0-ARC-001 row 13).
    public IReadOnlyCollection<string> DependsOn => ["Orders"];

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
        // V1-RMD-130: the real network transport (everything above this line
        // was already registered but never reached a physical printer).
        context.RegisterTransient<IPrinterTransport, TcpEscPosPrinterTransport>();
    }
}
