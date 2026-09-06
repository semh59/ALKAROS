using ALKAROS.ModuleComposition;
using ALKAROS.Production.BatchLifecycle;
using ALKAROS.Production.StockEffects;

namespace ALKAROS.Production;

/// <summary>
/// Composition module for production batch lifecycle and its stock effects
/// (V1.1: production).
/// </summary>
public sealed class ProductionModule : IModule
{
    public string Id => "Production";

    public string DisplayName => "Production";

    // Batch completion posts consumption/output stock effects into Inventory's
    // own schema within the same transaction (V0-ARC-001 row 11: Production →
    // Inventory, portion output) — a direct-call edge, not an integration
    // event, since the batch's own atomicity depends on the stock check.
    public IReadOnlyCollection<string> DependsOn => ["Inventory"];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IProductionBatchRepository, PostgresProductionBatchRepository>();
        context.RegisterTransient<IProductionBatchService, ProductionBatchService>();
        context.RegisterTransient<IProductionStockEffectService, ProductionStockEffectService>();
    }
}
