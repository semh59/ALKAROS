using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ALKAROS.Recipes.Units;
using ALKAROS.Recipes.Versioning;

namespace ALKAROS.Recipes.CostSnapshots;

public sealed record CreateSnapshotCommand(
    Guid RecipeVersionId,
    DateOnly CostBasisDate,
    IReadOnlyDictionary<Guid, string>? StockItemUnits = null,
    IReadOnlyDictionary<Guid, decimal>? FallbackItemCosts = null,
    string Currency = "TRY");

public interface IRecipeCostSnapshotService
{
    Task<RecipeCostSnapshot> CreateSnapshotAsync(CreateSnapshotCommand command, CancellationToken ct = default);
    Task<RecipeCostSnapshot?> GetEffectiveSnapshotAsync(Guid recipeVersionId, DateOnly asOfDate, CancellationToken ct = default);
    Task<RecipeCostSnapshot> GetRequiredEffectiveSnapshotAsync(Guid recipeVersionId, DateOnly asOfDate, CancellationToken ct = default);
}

public sealed class RecipeCostSnapshotService : IRecipeCostSnapshotService
{
    private readonly IRecipeCostSnapshotRepository _snapshotRepo;
    private readonly IRecipeVersionRepository _versionRepo;
    private readonly IStockCostResolver _costResolver;
    private readonly IUnitConverter _unitConverter;

    public RecipeCostSnapshotService(
        IRecipeCostSnapshotRepository snapshotRepo,
        IRecipeVersionRepository versionRepo,
        IStockCostResolver costResolver,
        IUnitConverter unitConverter)
    {
        _snapshotRepo = snapshotRepo ?? throw new ArgumentNullException(nameof(snapshotRepo));
        _versionRepo = versionRepo ?? throw new ArgumentNullException(nameof(versionRepo));
        _costResolver = costResolver ?? throw new ArgumentNullException(nameof(costResolver));
        _unitConverter = unitConverter ?? throw new ArgumentNullException(nameof(unitConverter));
    }

    public async Task<RecipeCostSnapshot> CreateSnapshotAsync(CreateSnapshotCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var existing = await _snapshotRepo.GetByVersionAndDateAsync(command.RecipeVersionId, command.CostBasisDate, ct);
        if (existing != null)
        {
            throw new DuplicateCostSnapshotException(command.RecipeVersionId, command.CostBasisDate);
        }

        var version = await _versionRepo.GetByIdAsync(command.RecipeVersionId, ct)
            ?? throw new RecipeVersionNotFoundException(command.RecipeVersionId);

        var snapshot = RecipeCostSnapshot.Create(
            command.RecipeVersionId,
            command.CostBasisDate,
            command.Currency);

        foreach (var ingredient in version.Ingredients)
        {
            var stockItemId = ingredient.IngredientItemId;

            // 1. Loss percentage -> waste factor (V0-DOM-010: LossPercentage 5 -> 0.05)
            var wasteFactor = ingredient.LossPercentage / 100m;

            // 2. Determine stock tracking unit (defaults to native unit if not mapped)
            var stockUnit = command.StockItemUnits != null && command.StockItemUnits.TryGetValue(stockItemId, out var mappedUnit)
                ? mappedUnit
                : ingredient.UnitCode;

            // 3. Order-of-operations (CORR:C9, V0-DOM-010):
            // Effective native quantity = raw_quantity * (1 + waste_factor)
            var effNativeQty = Math.Round(ingredient.Quantity * (1m + wasteFactor), 4, MidpointRounding.AwayFromZero);

            // 4. Unit conversion to stock item's tracking unit
            var stockQty = Math.Round(_unitConverter.Convert(effNativeQty, ingredient.UnitCode, stockUnit), 4, MidpointRounding.AwayFromZero);

            // 5. Moving-average cost resolution
            decimal? resolvedCost = await _costResolver.ResolveMovingAverageCostAsync(stockItemId, command.CostBasisDate, ct);
            if (resolvedCost == null && command.FallbackItemCosts != null && command.FallbackItemCosts.TryGetValue(stockItemId, out var fallbackCost))
            {
                resolvedCost = fallbackCost;
            }

            if (resolvedCost == null)
            {
                throw new MissingCostBasisException(stockItemId, command.CostBasisDate);
            }

            var item = RecipeCostSnapshotItem.Create(
                snapshotId: snapshot.Id,
                stockItemId: stockItemId,
                rawQuantity: ingredient.Quantity,
                wasteFactor: wasteFactor,
                nativeUnitCode: ingredient.UnitCode,
                stockQuantity: stockQty,
                stockUnitCode: stockUnit,
                unitCost: resolvedCost.Value);

            snapshot.AddItem(item);
        }

        await _snapshotRepo.SaveAsync(snapshot, ct);
        return snapshot;
    }

    public async Task<RecipeCostSnapshot?> GetEffectiveSnapshotAsync(Guid recipeVersionId, DateOnly asOfDate, CancellationToken ct = default)
    {
        return await _snapshotRepo.GetEffectiveSnapshotAsync(recipeVersionId, asOfDate, ct);
    }

    public async Task<RecipeCostSnapshot> GetRequiredEffectiveSnapshotAsync(Guid recipeVersionId, DateOnly asOfDate, CancellationToken ct = default)
    {
        var snapshot = await GetEffectiveSnapshotAsync(recipeVersionId, asOfDate, ct);
        if (snapshot == null)
        {
            throw new RecipeCostSnapshotNotFoundException(recipeVersionId, asOfDate);
        }
        return snapshot;
    }
}
