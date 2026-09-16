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
        context.RegisterTransient<IUnitConverter, UnitConverter>();
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
