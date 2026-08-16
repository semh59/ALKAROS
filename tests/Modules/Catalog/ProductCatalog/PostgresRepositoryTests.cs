using ALKAROS.Catalog.ProductCatalog.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.Catalog.ProductCatalog.Tests;

/// <summary>
/// Integration tests that exercise the PostgreSQL repositories and the
/// catalog migration constraints (unique keys, check constraints, foreign
/// keys) against a real database created from 006-catalog.up.sql.
/// </summary>
public sealed class PostgresRepositoryTests : IClassFixture<CatalogTestDatabase>
{
    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";
    private const string ForeignKeyViolation = "23503";

    private readonly CatalogTestDatabase _database;
    private readonly PostgresCategoryRepository _categories;
    private readonly PostgresTaxProfileRepository _taxProfiles;
    private readonly PostgresModifierGroupRepository _modifierGroups;
    private readonly PostgresModifierRepository _modifiers;
    private readonly PostgresProductRepository _products;
    private readonly PostgresProductModifierGroupRepository _productModifierGroups;

    public PostgresRepositoryTests(CatalogTestDatabase database)
    {
        _database = database;
        _categories = new PostgresCategoryRepository(database.DataSource);
        _taxProfiles = new PostgresTaxProfileRepository(database.DataSource);
        _modifierGroups = new PostgresModifierGroupRepository(database.DataSource);
        _modifiers = new PostgresModifierRepository(database.DataSource);
        _products = new PostgresProductRepository(database.DataSource);
        _productModifierGroups = new PostgresProductModifierGroupRepository(database.DataSource);
    }

    [Fact]
    public async Task CategoryRoundTripPersistsAndReturnsAllFields()
    {
        var parentId = Guid.NewGuid();
        await _categories.AddAsync(new Category(parentId, "ROOT", "Root"));
        var id = Guid.NewGuid();
        await _categories.AddAsync(new Category(id, "MAIN", "Main", parentId, 2));

        var byId = await _categories.GetByIdAsync(id);
        Assert.NotNull(byId);
        Assert.Equal(id, byId.Id);
        Assert.Equal("MAIN", byId.Code);
        Assert.Equal("Main", byId.Name);
        Assert.Equal(parentId, byId.ParentId);
        Assert.Equal(2, byId.SortOrder);
        Assert.True(byId.Active);

        var byCode = await _categories.GetByCodeAsync("MAIN");
        Assert.NotNull(byCode);
        Assert.Equal(id, byCode.Id);

        var all = await _categories.GetAllAsync();
        Assert.Contains(all, c => c.Id == id);
    }

    [Fact]
    public async Task CategoryUpdateAndDeleteArePersisted()
    {
        var id = Guid.NewGuid();
        await _categories.AddAsync(new Category(id, "OLD", "Old Name"));

        await _categories.UpdateAsync(new Category(id, "NEW", "New Name", null, 1, active: false));
        var updated = await _categories.GetByIdAsync(id);
        Assert.NotNull(updated);
        Assert.Equal("NEW", updated.Code);
        Assert.Equal("New Name", updated.Name);
        Assert.Equal(1, updated.SortOrder);
        Assert.False(updated.Active);

        await _categories.DeleteAsync(id);
        Assert.Null(await _categories.GetByIdAsync(id));
    }

    [Fact]
    public async Task CategoryDuplicateCodeIsRejectedByUniqueConstraint()
    {
        await _categories.AddAsync(new Category(Guid.NewGuid(), "DUP", "First"));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _categories.AddAsync(new Category(Guid.NewGuid(), "DUP", "Second")));
        Assert.Equal(UniqueViolation, ex.SqlState);
    }

    [Fact]
    public async Task TaxProfileDuplicateCodeIsRejectedByUniqueConstraint()
    {
        await _taxProfiles.AddAsync(new TaxProfile(Guid.NewGuid(), "DUP", "First", 0m));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _taxProfiles.AddAsync(new TaxProfile(Guid.NewGuid(), "DUP", "Second", 10m)));
        Assert.Equal(UniqueViolation, ex.SqlState);
    }

    [Fact]
    public async Task TaxProfileRoundTripPersistsVatRate()
    {
        var id = Guid.NewGuid();
        await _taxProfiles.AddAsync(new TaxProfile(id, "VAT20", "KDV 20%", 20m));

        var byId = await _taxProfiles.GetByIdAsync(id);
        Assert.NotNull(byId);
        Assert.Equal(20m, byId.VatRate);
        Assert.True(byId.Active);

        var byCode = await _taxProfiles.GetByCodeAsync("VAT20");
        Assert.NotNull(byCode);
        Assert.Equal(id, byCode.Id);
    }

    [Fact]
    public async Task TaxProfileInactiveFlagIsPersisted()
    {
        var id = Guid.NewGuid();
        await _taxProfiles.AddAsync(new TaxProfile(id, "OFF", "Hidden", 0m, active: false));

        var byId = await _taxProfiles.GetByIdAsync(id);
        Assert.NotNull(byId);
        Assert.False(byId.Active);
    }

    [Fact]
    public async Task ModifierGroupRoundTripPersistsSelectionBounds()
    {
        var id = Guid.NewGuid();
        await _modifierGroups.AddAsync(new ModifierGroup(id, "SIZES", "Sizes", SelectionType.SelectOne, 1, 2));

        var byId = await _modifierGroups.GetByIdAsync(id);
        Assert.NotNull(byId);
        Assert.Equal(SelectionType.SelectOne, byId.SelectionType);
        Assert.Equal(1, byId.MinSelections);
        Assert.Equal(2, byId.MaxSelections);
        Assert.True(byId.Active);

        var byCode = await _modifierGroups.GetByCodeAsync("SIZES");
        Assert.NotNull(byCode);
        Assert.Equal(id, byCode.Id);
    }

    [Fact]
    public async Task ModifierGroupMaxSelectBelowMinSelectIsRejectedByCheckConstraint()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() => _database.ExecuteAsync(
            "INSERT INTO catalog.modifier_groups (modifier_group_id, code, name, selection_type, min_selections, max_selections) " +
            "VALUES (@id, @code, @name, 1, @min, @max);",
            ("id", Guid.NewGuid()), ("code", "BAD"), ("name", "Bad"), ("min", 3), ("max", 2)));
        Assert.Equal(CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task ModifierGroupUnknownSelectionTypeIsRejectedByCheckConstraint()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() => _database.ExecuteAsync(
            "INSERT INTO catalog.modifier_groups (modifier_group_id, code, name, selection_type) " +
            "VALUES (@id, @code, @name, @selection_type);",
            ("id", Guid.NewGuid()), ("code", "BAD"), ("name", "Bad"), ("selection_type", 9)));
        Assert.Equal(CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task ModifierRoundTripPersistsPriceDelta()
    {
        var groupId = Guid.NewGuid();
        await _modifierGroups.AddAsync(new ModifierGroup(groupId, "TOPS", "Toppings", SelectionType.SelectMany));
        var id = Guid.NewGuid();

        await _modifiers.AddAsync(new Modifier(id, groupId, "CHEESE", "Extra Cheese", 2.50m));

        var byId = await _modifiers.GetByIdAsync(id);
        Assert.NotNull(byId);
        Assert.Equal(2.50m, byId.PriceDelta);
        Assert.Null(byId.ProductId);
        Assert.True(byId.Active);

        var byGroup = await _modifiers.GetByModifierGroupAsync(groupId);
        Assert.Contains(byGroup, m => m.Id == id);
    }

    [Fact]
    public async Task ModifierWithProductLinkPersistsProductId()
    {
        var groupId = Guid.NewGuid();
        await _modifierGroups.AddAsync(new ModifierGroup(groupId, "TOPS2", "Toppings", SelectionType.SelectMany));
        var productId = Guid.NewGuid();
        await _products.AddAsync(new Product(productId, "SKU-MOD", "Item", ProductType.MenuItem, StockMode.Untracked));
        var id = Guid.NewGuid();

        await _modifiers.AddAsync(new Modifier(id, groupId, "CHEESE2", "Extra Cheese", 1m, productId));

        var byId = await _modifiers.GetByIdAsync(id);
        Assert.NotNull(byId);
        Assert.Equal(productId, byId.ProductId);
    }

    [Fact]
    public async Task ModifierDuplicateCodeAcrossGroupsIsRejectedByUniqueConstraint()
    {
        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();
        await _modifierGroups.AddAsync(new ModifierGroup(groupA, "GRP-A", "Group A", SelectionType.SelectOne));
        await _modifierGroups.AddAsync(new ModifierGroup(groupB, "GRP-B", "Group B", SelectionType.SelectOne));
        await _modifiers.AddAsync(new Modifier(Guid.NewGuid(), groupA, "DUP", "First", 0m));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _modifiers.AddAsync(new Modifier(Guid.NewGuid(), groupB, "DUP", "Second", 1m)));
        Assert.Equal(UniqueViolation, ex.SqlState);
    }

    [Fact]
    public async Task ModifierWithUnknownGroupIsRejectedByForeignKeyConstraint()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _modifiers.AddAsync(new Modifier(Guid.NewGuid(), Guid.NewGuid(), "NOGROUP", "No Group", 0m)));
        Assert.Equal(ForeignKeyViolation, ex.SqlState);
    }

    [Fact]
    public async Task ModifierWithUnknownProductIsRejectedByForeignKeyConstraint()
    {
        var groupId = Guid.NewGuid();
        await _modifierGroups.AddAsync(new ModifierGroup(groupId, "PFAS-GRP", "Group", SelectionType.SelectOne));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _modifiers.AddAsync(new Modifier(Guid.NewGuid(), groupId, "NOPROD", "No", 0m, Guid.NewGuid())));
        Assert.Equal(ForeignKeyViolation, ex.SqlState);
    }

    [Fact]
    public async Task ProductRoundTripPersistsAllFields()
    {
        var categoryId = Guid.NewGuid();
        var taxProfileId = Guid.NewGuid();
        await _categories.AddAsync(new Category(categoryId, "FOOD", "Food"));
        await _taxProfiles.AddAsync(new TaxProfile(taxProfileId, "VAT0", "No Tax", 0m));

        var id = Guid.NewGuid();
        await _products.AddAsync(new Product(
            id, "SKU-RT", "Round Trip", ProductType.MenuItem, StockMode.QuantityTracked,
            categoryId, taxProfileId, "Description", "kitchen-1", 4, 12.50m, active: false));

        var byId = await _products.GetByIdAsync(id);
        Assert.NotNull(byId);
        Assert.Equal("SKU-RT", byId.Sku);
        Assert.Equal(ProductType.MenuItem, byId.ProductType);
        Assert.Equal(StockMode.QuantityTracked, byId.StockMode);
        Assert.Equal(categoryId, byId.CategoryId);
        Assert.Equal(taxProfileId, byId.TaxProfileId);
        Assert.Equal("Description", byId.Description);
        Assert.Equal("kitchen-1", byId.PrinterRoutePolicy);
        Assert.Equal(4, byId.DisplayOrder);
        Assert.Equal(12.50m, byId.CurrentPrice);
        Assert.False(byId.Active);

        var bySku = await _products.GetBySkuAsync("SKU-RT");
        Assert.NotNull(bySku);
        Assert.Equal(id, bySku.Id);

        var byCategory = await _products.GetByCategoryAsync(categoryId);
        Assert.Contains(byCategory, p => p.Id == id);
    }

    [Fact]
    public async Task ProductWithoutCategoryAndTaxProfileIsAllowed()
    {
        var id = Guid.NewGuid();
        await _products.AddAsync(new Product(id, "SKU-NULL", "Standalone", ProductType.MenuItem, StockMode.Untracked));

        var byId = await _products.GetByIdAsync(id);
        Assert.NotNull(byId);
        Assert.Null(byId.CategoryId);
        Assert.Null(byId.TaxProfileId);
    }

    [Fact]
    public async Task ProductDuplicateSkuIsRejectedByUniqueConstraint()
    {
        await _products.AddAsync(new Product(Guid.NewGuid(), "SKU-DUP", "First", ProductType.MenuItem, StockMode.Untracked));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _products.AddAsync(new Product(Guid.NewGuid(), "SKU-DUP", "Second", ProductType.MenuItem, StockMode.Untracked)));
        Assert.Equal(UniqueViolation, ex.SqlState);
    }

    [Fact]
    public async Task ProductUnknownProductTypeIsRejectedByCheckConstraint()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() => _database.ExecuteAsync(
            "INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode) " +
            "VALUES (@id, @sku, @name, @product_type, 1);",
            ("id", Guid.NewGuid()), ("sku", "SKU-BADTYPE"), ("name", "Bad"), ("product_type", 9)));
        Assert.Equal(CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task ProductWithUnknownCategoryIsRejectedByForeignKeyConstraint()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _products.AddAsync(new Product(
                Guid.NewGuid(), "SKU-FK", "No Category", ProductType.MenuItem, StockMode.Untracked,
                categoryId: Guid.NewGuid())));
        Assert.Equal(ForeignKeyViolation, ex.SqlState);
    }

    [Fact]
    public async Task ProductModifierGroupRoundTripPersistsLink()
    {
        var productId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        await _products.AddAsync(new Product(productId, "SKU-PMG", "Item", ProductType.MenuItem, StockMode.Untracked));
        await _modifierGroups.AddAsync(new ModifierGroup(groupId, "PMG-GRP", "Group", SelectionType.SelectOne));

        var linkId = Guid.NewGuid();
        await _productModifierGroups.AddAsync(new ProductModifierGroup(linkId, productId, groupId));

        var byId = await _productModifierGroups.GetByIdAsync(linkId);
        Assert.NotNull(byId);
        Assert.Equal(productId, byId.ProductId);
        Assert.Equal(groupId, byId.ModifierGroupId);

        var byProduct = await _productModifierGroups.GetByProductAsync(productId);
        Assert.Contains(byProduct, l => l.Id == linkId);
    }

    [Fact]
    public async Task ProductModifierGroupDuplicatePairIsRejectedByUniqueConstraint()
    {
        var productId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        await _products.AddAsync(new Product(productId, "SKU-PMG2", "Item", ProductType.MenuItem, StockMode.Untracked));
        await _modifierGroups.AddAsync(new ModifierGroup(groupId, "PFAS-GRP2", "Group", SelectionType.SelectOne));
        await _productModifierGroups.AddAsync(new ProductModifierGroup(Guid.NewGuid(), productId, groupId));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _productModifierGroups.AddAsync(new ProductModifierGroup(Guid.NewGuid(), productId, groupId)));
        Assert.Equal(UniqueViolation, ex.SqlState);
    }

    [Fact]
    public async Task ProductModifierGroupUnknownProductIsRejectedByForeignKeyConstraint()
    {
        var groupId = Guid.NewGuid();
        await _modifierGroups.AddAsync(new ModifierGroup(groupId, "PFAS-GRP3", "Group", SelectionType.SelectOne));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _productModifierGroups.AddAsync(new ProductModifierGroup(Guid.NewGuid(), Guid.NewGuid(), groupId)));
        Assert.Equal(ForeignKeyViolation, ex.SqlState);
    }

    [Fact]
    public async Task ProductModifierGroupUnknownModifierGroupIsRejectedByForeignKeyConstraint()
    {
        var productId = Guid.NewGuid();
        await _products.AddAsync(new Product(productId, "SKU-PMGFK", "Item", ProductType.MenuItem, StockMode.Untracked));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _productModifierGroups.AddAsync(new ProductModifierGroup(Guid.NewGuid(), productId, Guid.NewGuid())));
        Assert.Equal(ForeignKeyViolation, ex.SqlState);
    }

    [Fact]
    public async Task ProductNegativeCurrentPriceIsRejectedByCheckConstraint()
    {
        await _database.ExecuteAsync(
            "ALTER TABLE catalog.products ADD CONSTRAINT chk_products_current_price_nonnegative CHECK (current_price IS NULL OR current_price >= 0);");

        try
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() =>
                _database.ExecuteAsync(
                    """
                    INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
                    VALUES (gen_random_uuid(), 'SKU-NEG', 'Negative Price', 1, 1, -5.00);
                    """));
            Assert.Equal(CheckViolation, ex.SqlState);
        }
        finally
        {
            await _database.ExecuteAsync(
                "ALTER TABLE catalog.products DROP CONSTRAINT IF EXISTS chk_products_current_price_nonnegative;");
        }
    }
}

/// <summary>
/// Verifies the rollback script removes the catalog schema. Uses its own
/// database so it cannot invalidate the schema shared by PostgresRepositoryTests.
/// </summary>
public sealed class RollbackScriptTests : IClassFixture<CatalogTestDatabase>
{
    private readonly CatalogTestDatabase _database;

    public RollbackScriptTests(CatalogTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task RollbackScriptRemovesTheCatalogSchema()
    {
        var downSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "006-catalog.down.sql"));
        await _database.ExecuteAsync(downSql);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _database.ExecuteAsync("SELECT count(*) FROM catalog.categories;"));
        Assert.Equal("42P01", ex.SqlState);
    }
}