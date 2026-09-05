namespace ALKAROS.Menu.DailyMenuLifecycle;

public interface IRecipeVersionValidator
{
    Task<bool> IsRecipeVersionActiveAsync(Guid recipeVersionId, CancellationToken ct = default);
}
