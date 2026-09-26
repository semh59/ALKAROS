using ALKAROS.Measurements;
using ALKAROS.ModuleComposition;
using ALKAROS.Recipes.CatalogMapping;
using ALKAROS.Recipes.CostSnapshots;
using ALKAROS.Recipes.TheoreticalConsumption;
using ALKAROS.Recipes.Units;
using ALKAROS.Recipes.Versioning;

namespace ALKAROS.Recipes;

/// <summary>
/// Composition module for immutable recipe versions, dimension-safe unit
/// conversion persistence and recipe cost snapshots (V1.1: units-recipes).
/// </summary>
public sealed class RecipesModule : IModule
{
    public string Id => "Recipes";

    public string DisplayName => "Recipes";

    // No direct-call edge: Recipes' own conversion logic lives in the shared
    // ALKAROS.Measurements building block, not in another module.
    public IReadOnlyCollection<string> DependsOn => Array.Empty<string>();

    public void Register(ModuleContext context)
    {
        // V1-RMD-319 (independent 2026-09-26 audit, finding K7): Transient before this fix meant every
        // resolution got a fresh, StandardUnits-only instance with no memory of any custom conversion
        // another caller had registered - Singleton is required for RegisterConversion to mean anything
        // across the whole running Host. See UnitConversionLoaderHostedService's own doc comment for the
        // full fix (startup load + live-register-on-write).
        context.RegisterSingleton<IUnitConverter, UnitConverter>();
        context.RegisterTransient<IUnitConversionRepository, PostgresUnitConversionRepository>();

        context.RegisterTransient<IRecipeRepository, PostgresRecipeRepository>();
        context.RegisterTransient<IRecipeVersionRepository, PostgresRecipeVersionRepository>();
        context.RegisterTransient<IRecipeLifecycleService, RecipeLifecycleService>();

        context.RegisterTransient<IRecipeCostSnapshotRepository, PostgresRecipeCostSnapshotRepository>();
        context.RegisterTransient<IStockCostResolver, PostgresStockCostResolver>();
        context.RegisterTransient<IRecipeCostSnapshotService, RecipeCostSnapshotService>();

        context.RegisterTransient<IProductRecipeMappingRepository, PostgresProductRecipeMappingRepository>();
        context.RegisterTransient<ITheoreticalConsumptionRecordRepository, PostgresTheoreticalConsumptionRecordRepository>();
    }
}
