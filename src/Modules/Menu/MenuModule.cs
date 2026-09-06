using ALKAROS.Menu.CounterProjection;
using ALKAROS.Menu.DailyMenuLifecycle;
using ALKAROS.Menu.StaticMenu;
using ALKAROS.ModuleComposition;

namespace ALKAROS.Menu;

/// <summary>
/// Composition module for the static menu catalog, the daily menu lifecycle
/// and its counter projection (V1.1: daily-menu).
/// </summary>
public sealed class MenuModule : IModule
{
    public string Id => "Menu";

    public string DisplayName => "Menu";

    // No direct-call edge: Menu reads catalog products and recipe versions
    // through its own repositories (ICatalogProductReader,
    // ICatalogProductPriceReader, IRecipeVersionValidator), not a project
    // reference to Catalog or Recipes.
    public IReadOnlyCollection<string> DependsOn => Array.Empty<string>();

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<ICatalogProductReader, PostgresCatalogProductReader>();
        context.RegisterTransient<IMenuRepository, PostgresMenuRepository>();
        context.RegisterTransient<IMenuItemRepository, PostgresMenuItemRepository>();
        context.RegisterTransient<IStaticMenuService, StaticMenuService>();

        context.RegisterTransient<ICatalogProductPriceReader, PostgresCatalogProductPriceReader>();
        context.RegisterTransient<IRecipeVersionValidator, PostgresRecipeVersionValidator>();
        context.RegisterTransient<IBusinessDateProvider, BusinessDateProvider>();
        context.RegisterTransient<IDailyMenuRepository, PostgresDailyMenuRepository>();
        context.RegisterTransient<IDailyMenuItemRepository, PostgresDailyMenuItemRepository>();
        context.RegisterTransient<IDailyMenuItemHistoryRepository, PostgresDailyMenuItemHistoryRepository>();
        context.RegisterTransient<IDailyMenuService, DailyMenuService>();

        context.RegisterTransient<IDailyMenuCounterProjector, PostgresDailyMenuCounterProjector>();
    }
}
