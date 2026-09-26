using ALKAROS.ModuleComposition;
using ALKAROS.Reconciliation.CaseFoundation;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ALKAROS.Reconciliation.OnlineOrders;

/// <summary>
/// Registers the V12-REC-001 scanner and case actions with their fixed list of online order source
/// pairs. Separate from <see cref="ReconciliationModule"/> for the same reason as V13-REC-001's
/// PaymentReconciliationModule; its list is registered under its own type so it never collides with the
/// payment source pairs. <see cref="IProviderEventReprocessing"/> is supplied by the Host, which binds it
/// to the OnlineOrdering contract. Registered in <c>ModuleRegistry.DefaultCatalog</c>.
/// </summary>
public sealed class OnlineOrderReconciliationModule : IModule
{
    public string Id => "Reconciliation.OnlineOrders";
    public string DisplayName => "Online Order Reconciliation";

    public IReadOnlyCollection<string> DependsOn => ["Reconciliation"];

    public void Register(ModuleContext context)
    {
        context.RegisterSingleton<IReadOnlyList<IOnlineOrderSourcePair>>(provider =>
        {
            var dataSource = provider.GetRequiredService<NpgsqlDataSource>();
            return new List<IOnlineOrderSourcePair>
            {
                new ProviderAcceptedLocallyRefusedSourcePair(dataSource),
                new LocallyAcceptedProviderUnknownSourcePair(dataSource),
                new ProviderEventFailedSourcePair(dataSource),
                new CancelledAfterHandoverSourcePair(dataSource),
                new AvailabilityNotDeliveredSourcePair(dataSource),
                new ProviderTotalMismatchSourcePair(dataSource),
                new ProviderStatusUnknownSourcePair(dataSource),
                new ProviderPriceMismatchSourcePair(dataSource),
                new ProviderPollingFailingSourcePair(dataSource),
            };
        });
        context.RegisterTransient(provider => new OnlineOrderReconciliationScanner(
            provider.GetRequiredService<IReadOnlyList<IOnlineOrderSourcePair>>(),
            provider.GetRequiredService<IReconciliationService>()));
        context.RegisterTransient(provider => new OnlineOrderReconciliationActions(
            provider.GetRequiredService<NpgsqlDataSource>(),
            provider.GetRequiredService<IReadOnlyList<IOnlineOrderSourcePair>>(),
            provider.GetRequiredService<IReconciliationService>(),
            provider.GetRequiredService<IProviderEventReprocessing>()));
    }
}
