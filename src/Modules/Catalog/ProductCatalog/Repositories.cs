namespace ALKAROS.Catalog.ProductCatalog;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Category?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Category category, CancellationToken cancellationToken = default);
    Task UpdateAsync(Category category, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface ITaxProfileRepository
{
    Task<TaxProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaxProfile?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaxProfile>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(TaxProfile taxProfile, CancellationToken cancellationToken = default);
    Task UpdateAsync(TaxProfile taxProfile, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Product product, CancellationToken cancellationToken = default);
    Task UpdateAsync(Product product, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IModifierGroupRepository
{
    Task<ModifierGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ModifierGroup?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModifierGroup>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ModifierGroup modifierGroup, CancellationToken cancellationToken = default);
    Task UpdateAsync(ModifierGroup modifierGroup, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IModifierRepository
{
    Task<Modifier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Modifier>> GetByModifierGroupAsync(Guid modifierGroupId, CancellationToken cancellationToken = default);
    Task AddAsync(Modifier modifier, CancellationToken cancellationToken = default);
    Task UpdateAsync(Modifier modifier, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IProductModifierGroupRepository
{
    Task<ProductModifierGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductModifierGroup>> GetByProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task AddAsync(ProductModifierGroup productModifierGroup, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}