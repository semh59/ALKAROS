using ALKAROS.Measurements;
using ALKAROS.Recipes.Units;
using ALKAROS.Recipes.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ALKAROS.Host.Experience.Recipes;

/// <summary>
/// V1-RMD-275: the recipe catalog itself. The product-to-recipe mapping surface
/// (V11-RCP-003) and the cost snapshot surface (V1-RMD-247) both need a recipe to
/// exist, but nothing in the running host could create one: RecipeLifecycleService
/// (V11-RCP-001, immutable versioned recipes) and the unit conversion repository
/// (V11-UNT-001) were registered and tested but had no caller. Mounted on the
/// manager-only recipes group (inventory.manage) next to the mapping routes.
/// A version is a Draft until activated; an activated or locked version is
/// immutable, so cost snapshots and consumption records never change under it.
/// </summary>
public static class RecipeManagementEndpoints
{
    public static RouteGroupBuilder MapRecipeManagement(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/", async (IRecipeRepository recipes, CancellationToken cancellationToken) =>
            Results.Ok((await recipes.GetAllAsync(cancellationToken)).Select(RecipeV1.From).ToArray()));

        group.MapPost("/", async (CreateRecipeV1 request, IRecipeLifecycleService lifecycle, CancellationToken cancellationToken) =>
        {
            var created = await lifecycle.CreateRecipeAsync(request.Code, request.Name, request.Description, cancellationToken);
            return Results.Created($"/api/v1/management/recipes/{created.Id:D}", RecipeV1.From(created));
        });

        group.MapGet("/{recipeId:guid}/versions", async (
            Guid recipeId, IRecipeRepository recipes, IRecipeVersionRepository versions, CancellationToken cancellationToken) =>
        {
            if (await recipes.GetByIdAsync(recipeId, cancellationToken) is null)
                throw new RecipeNotFoundException(recipeId);
            return Results.Ok((await versions.GetAllVersionsAsync(recipeId, cancellationToken)).Select(RecipeVersionV1.From).ToArray());
        });

        group.MapPost("/{recipeId:guid}/versions", async (
            Guid recipeId, CreateRecipeDraftV1 request, IRecipeLifecycleService lifecycle, CancellationToken cancellationToken) =>
        {
            var draft = await lifecycle.CreateInitialDraftVersionAsync(
                recipeId, request.YieldQuantity, request.YieldUnitCode ?? string.Empty,
                request.PreparationMinutes, request.Instructions, cancellationToken);
            return Results.Created($"/api/v1/management/recipes/{recipeId:D}/versions", RecipeVersionV1.From(draft));
        });

        group.MapPost("/{recipeId:guid}/versions/next", async (
            Guid recipeId, IRecipeLifecycleService lifecycle, CancellationToken cancellationToken) =>
        {
            var draft = await lifecycle.CreateNextVersionDraftAsync(recipeId, cancellationToken);
            return Results.Created($"/api/v1/management/recipes/{recipeId:D}/versions", RecipeVersionV1.From(draft));
        });

        group.MapPost("/versions/{versionId:guid}/ingredients", async (
            Guid versionId, AddRecipeIngredientV1 request, IRecipeLifecycleService lifecycle, CancellationToken cancellationToken) =>
        {
            var updated = await lifecycle.AddIngredientToDraftAsync(
                versionId, request.IngredientItemId, request.Quantity, request.UnitCode ?? string.Empty,
                request.LossPercentage, request.SortOrder, request.Notes, cancellationToken);
            return Results.Ok(RecipeVersionV1.From(updated));
        });

        group.MapDelete("/versions/{versionId:guid}/ingredients/{ingredientItemId:guid}", async (
            Guid versionId, Guid ingredientItemId, IRecipeLifecycleService lifecycle, CancellationToken cancellationToken) =>
            Results.Ok(RecipeVersionV1.From(await lifecycle.RemoveIngredientFromDraftAsync(versionId, ingredientItemId, cancellationToken))));

        group.MapPost("/{recipeId:guid}/versions/{versionNumber:int}/activate", async (
            Guid recipeId, int versionNumber, IRecipeLifecycleService lifecycle, CancellationToken cancellationToken) =>
        {
            await lifecycle.ActivateVersionAsync(recipeId, versionNumber, null, cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/versions/{versionId:guid}/lock", async (
            Guid versionId, IRecipeLifecycleService lifecycle, CancellationToken cancellationToken) =>
        {
            await lifecycle.LockVersionForOperationalUseAsync(versionId, cancellationToken);
            return Results.NoContent();
        });

        // Custom unit conversions (for example one case = 12 pieces) the built-in
        // dimension-safe converter does not know.
        group.MapGet("/unit-conversions", async (IUnitConversionRepository conversions, CancellationToken cancellationToken) =>
            Results.Ok((await conversions.GetActiveConversionsAsync(cancellationToken)).Select(UnitConversionV1.From).ToArray()));

        group.MapPost("/unit-conversions", async (
            CreateUnitConversionV1 request,
            IUnitConversionRepository conversions,
            IUnitConverter converter,
            CancellationToken cancellationToken) =>
        {
            var conversion = new UnitConversion(Guid.NewGuid(), request.FromUnitCode ?? string.Empty, request.ToUnitCode ?? string.Empty, request.Factor);
            // V1-RMD-319 (K7): validated against the shared runtime converter BEFORE persisting - a
            // contradictory pair (e.g. an incompatible reverse factor already registered) rejects here,
            // before any database write, rather than leaving a persisted row that
            // UnitConversionLoaderHostedService would then have to skip on every future startup. Before
            // this fix, nothing anywhere ever called RegisterConversion, so a stored conversion never
            // affected a real goods-receipt/stock-count/production-consumption calculation at all.
            converter.RegisterConversion(conversion.FromUnitCode, conversion.ToUnitCode, conversion.Factor);
            await conversions.AddConversionAsync(conversion, cancellationToken);
            return Results.Created("/api/v1/management/recipes/unit-conversions", UnitConversionV1.From(conversion));
        });

        return group;
    }
}

public sealed record CreateRecipeV1(string Code, string Name, string? Description = null);

public sealed record CreateRecipeDraftV1(decimal YieldQuantity, string YieldUnitCode, int PreparationMinutes = 0, string? Instructions = null);

public sealed record AddRecipeIngredientV1(
    Guid IngredientItemId, decimal Quantity, string UnitCode, decimal LossPercentage = 0m, int SortOrder = 0, string? Notes = null);

public sealed record CreateUnitConversionV1(string FromUnitCode, string ToUnitCode, decimal Factor);

public sealed record RecipeV1(Guid Id, string Code, string Name, string? Description, DateTimeOffset CreatedAt)
{
    public static RecipeV1 From(Recipe recipe) => new(recipe.Id, recipe.Code, recipe.Name, recipe.Description, recipe.CreatedAt);
}

public sealed record RecipeIngredientV1(
    Guid IngredientItemId, decimal Quantity, string UnitCode, decimal LossPercentage, int SortOrder, string? Notes)
{
    public static RecipeIngredientV1 From(RecipeIngredientItem item)
        => new(item.IngredientItemId, item.Quantity, item.UnitCode, item.LossPercentage, item.SortOrder, item.Notes);
}

public sealed record RecipeVersionV1(
    Guid Id,
    Guid RecipeId,
    int VersionNumber,
    string Status,
    decimal YieldQuantity,
    string YieldUnitCode,
    int PreparationMinutes,
    string? Instructions,
    bool IsLocked,
    DateTimeOffset? ActivatedAt,
    IReadOnlyList<RecipeIngredientV1> Ingredients)
{
    public static RecipeVersionV1 From(RecipeVersion version) => new(
        version.Id, version.RecipeId, version.VersionNumber, version.Status.ToString(), version.YieldQuantity,
        version.YieldUnitCode, version.PreparationMinutes, version.Instructions, version.IsLocked, version.ActivatedAt,
        version.Ingredients.Select(RecipeIngredientV1.From).ToArray());
}

public sealed record UnitConversionV1(Guid Id, string FromUnitCode, string ToUnitCode, decimal Factor, bool Active)
{
    public static UnitConversionV1 From(UnitConversion conversion)
        => new(conversion.Id, conversion.FromUnitCode, conversion.ToUnitCode, conversion.Factor, conversion.Active);
}
