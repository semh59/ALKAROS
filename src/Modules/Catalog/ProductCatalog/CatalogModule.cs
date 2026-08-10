namespace ALKAROS.Catalog.ProductCatalog;

using ALKAROS.Catalog.Pricing;
using ALKAROS.ModuleComposition;

public sealed class CatalogModule : IModule
{
    public string Id => "Catalog";

    public string DisplayName => "Product Catalog";

    public IReadOnlyCollection<string> DependsOn => Array.Empty<string>();

    public void Register(ModuleContext context)
    {
        context
            .RegisterTransient<ICategoryRepository, PostgresCategoryRepository>()
            .RegisterTransient<ITaxProfileRepository, PostgresTaxProfileRepository>()
            .RegisterTransient<IProductRepository, PostgresProductRepository>()
            .RegisterTransient<IModifierGroupRepository, PostgresModifierGroupRepository>()
            .RegisterTransient<IModifierRepository, PostgresModifierRepository>()
            .RegisterTransient<IProductModifierGroupRepository, PostgresProductModifierGroupRepository>()
            .RegisterTransient<IPricingRepository, PostgresPricingRepository>();
    }
}