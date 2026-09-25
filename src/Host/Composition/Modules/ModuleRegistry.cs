using System.Reflection;
using ALKAROS.ModuleComposition;

namespace ALKAROS.Host.Composition.Modules;

/// <summary>
/// Loads modules from the given assemblies and composes them through
/// <see cref="ModuleCompositionRoot"/>. Only concrete <see cref="IModule"/>
/// implementations with a public parameterless constructor that are actually
/// present in the scanned ALKAROS assemblies get registered and loaded;
/// every other assembly is ignored.
/// </summary>
public static class ModuleRegistry
{
    /// <summary>
    /// The default executable module catalog containing every standard production ALKAROS module.
    /// </summary>
    public static readonly IReadOnlyList<Type> DefaultCatalog = [
        typeof(ALKAROS.Orders.OrderAggregate.OrdersModule),
        typeof(ALKAROS.Billing.BillFoundation.BillingModule),
        typeof(ALKAROS.Catalog.ProductCatalog.CatalogModule),
        typeof(ALKAROS.Tables.TableLifecycle.TablesModule),
        typeof(ALKAROS.Audit.AuditModule),
        typeof(ALKAROS.Cash.CashModule),
        typeof(ALKAROS.Identity.IdentityModule),
        typeof(ALKAROS.Kitchen.KitchenModule),
        typeof(ALKAROS.Observability.ObservabilityModule),
        // V15-SEC-001/002/003: must precede Operations (its OffsiteBackup
        // feature depends on Security's secret rotation).
        typeof(ALKAROS.Security.SecurityModule),
        typeof(ALKAROS.Operations.OperationsModule),
        typeof(ALKAROS.Reconciliation.ReconciliationModule),
        typeof(ALKAROS.Reporting.ReportingModule),
        typeof(ALKAROS.Settings.SettingsModule),
        typeof(ALKAROS.Support.SupportModule),
        typeof(ALKAROS.Recipes.RecipesModule),
        typeof(ALKAROS.Inventory.InventoryModule),
        typeof(ALKAROS.Menu.MenuModule),
        typeof(ALKAROS.Purchasing.PurchasingModule),
        typeof(ALKAROS.Production.ProductionModule),
        typeof(ALKAROS.QrOrdering.TokenLifecycle.QrOrderingModule),
        // V12-MAP-001: Online Ordering (row 20) - after Catalog, its only direct-call dependency.
        typeof(ALKAROS.OnlineOrdering.OnlineOrderingModule),
        typeof(ALKAROS.Payments.PaymentAggregate.PaymentAggregateModule),
        // V13-CSH-004: the Cash/Payments sub-modules below already existed
        // (V13-CSH-001/002/003, V13-ALC-001) but were never added here, so
        // none of their services were ever actually resolvable by the live
        // Host - this is the first HTTP surface that needs them.
        typeof(ALKAROS.Cash.SessionLifecycle.CashSessionLifecycleModule),
        typeof(ALKAROS.Cash.TransactionLedger.CashTransactionLedgerModule),
        typeof(ALKAROS.Payments.Allocations.Persistence.PaymentAllocationPersistenceModule),
        typeof(ALKAROS.Cash.TenderHandler.CashTenderHandlerModule),
        typeof(ALKAROS.Payments.CardSettlement.CardSettlementModule),
        typeof(ALKAROS.Payments.EftTender.EftTenderModule),
        typeof(ALKAROS.Payments.TenderRouting.TenderCompositionModule),
        typeof(ALKAROS.Reconciliation.Payments.PaymentReconciliationModule),
        typeof(ALKAROS.Billing.PaymentClosure.BillPaymentClosureModule)
    ];

    /// <summary>
    /// Returns every candidate <see cref="IModule"/> type declared in the
    /// scanned ALKAROS assemblies, in deterministic assembly order.
    /// </summary>
    public static IReadOnlyList<Type> Discover(IEnumerable<Assembly> assemblies)
    {
        var discovered = new List<Type>();

        foreach (var assembly in assemblies)
        {
            var assemblyName = assembly.GetName().Name;
            if (assemblyName is null || !assemblyName.StartsWith("ALKAROS.", StringComparison.Ordinal))
                continue;

            foreach (var type in assembly.GetExportedTypes())
            {
                if (type.IsAbstract || !typeof(IModule).IsAssignableFrom(type))
                    continue;

                if (type.GetConstructor(Type.EmptyTypes) is not null)
                    discovered.Add(type);
            }
        }

        return discovered;
    }

    /// <summary>
    /// Registers the given module types and returns the module ids in the
    /// validated topological composition order. Throws when the dependency
    /// graph is cyclic, references an unknown module, or declares a duplicate
    /// module id.
    /// </summary>
    public static IReadOnlyList<string> Compose(IEnumerable<Type> moduleTypes)
    {
        var root = BuildRoot(moduleTypes);
        return root.Compose().Select(module => module.Id).ToList();
    }

    /// <summary>
    /// Registers the given module types, runs the validated composition, and
    /// returns the composition root so callers can read the concrete service
    /// registrations produced by the modules.
    /// </summary>
    public static ModuleCompositionRoot ComposeRoot(IEnumerable<Type> moduleTypes)
    {
        var root = BuildRoot(moduleTypes);
        root.Compose();
        return root;
    }

    private static ModuleCompositionRoot BuildRoot(IEnumerable<Type> moduleTypes)
    {
        ArgumentNullException.ThrowIfNull(moduleTypes);

        var root = new ModuleCompositionRoot();

        foreach (var type in moduleTypes)
        {
            var constructor = type.GetConstructor(Type.EmptyTypes)
                ?? throw new InvalidOperationException(
                    $"Module '{type.FullName}' has no public parameterless constructor.");

            root.AddModule((IModule)constructor.Invoke(null));
        }

        return root;
    }
}
