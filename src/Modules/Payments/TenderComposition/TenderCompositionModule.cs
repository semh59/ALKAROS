using ALKAROS.Cash.TenderHandler;
using ALKAROS.ModuleComposition;
using ALKAROS.Payments.EftTender;
using Microsoft.Extensions.DependencyInjection;

namespace ALKAROS.Payments.TenderRouting;

/// <summary>
/// Registers the composed, fail-closed <see cref="ITenderHandlerRegistry"/>
/// and <see cref="TenderRouter"/> (V13-PAY-003's Goal) — separate from the
/// other Payments modules so this task never has to write to any of their
/// shared module files, same reasoning as
/// <see cref="ALKAROS.Payments.CardSettlement.CardSettlementModule"/>.
/// Registered in <c>ModuleRegistry.DefaultCatalog</c>.
///
/// The registry is built once, eagerly, from a singleton factory: any
/// duplicate/missing registration inside
/// <see cref="TenderHandlerRegistryFactory.Build"/> throws the moment
/// <see cref="ITenderHandlerRegistry"/> is first resolved — see
/// <c>TenderCompositionHostConstructabilityTests</c> for the real-Host proof
/// that this resolves cleanly today, and
/// <c>TenderHandlerRegistryFactoryTests</c> for the direct proof that a
/// duplicate/missing registration fails closed.
/// </summary>
public sealed class TenderCompositionModule : IModule
{
    public string Id => "Payments.TenderComposition";
    public string DisplayName => "Payments Tender Composition";

    public IReadOnlyCollection<string> DependsOn =>
        ["Payments", "Cash.TenderHandler", "Payments.EftTender"];

    public void Register(ModuleContext context)
    {
        context.RegisterSingleton<ITenderHandlerRegistry>(sp =>
            TenderHandlerRegistryFactory.Build(
                new CashTenderMethodAdapter(sp.GetRequiredService<ICashTenderHandler>()),
                new PendingBankCardTerminalIntegrationHandler(),
                sp.GetRequiredService<IEftTenderHandler>()));
        context.RegisterSingleton<TenderRouter, TenderRouter>();
    }
}
