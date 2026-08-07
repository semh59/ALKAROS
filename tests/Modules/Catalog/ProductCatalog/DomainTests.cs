namespace ALKAROS.Catalog.ProductCatalog.Tests;

using ALKAROS.Catalog.ProductCatalog;
using FluentAssertions;
using Xunit;

public class CategoryTests
{
    [Fact]
    public void ConstructorValidParametersCreatesCategory()
    {
        var id = Guid.NewGuid();
        var category = new Category(id, "FOOD", "Food Items", null, 1);

        category.Id.Should().Be(id);
        category.Code.Should().Be("FOOD");
        category.Name.Should().Be("Food Items");
        category.ParentId.Should().BeNull();
        category.SortOrder.Should().Be(1);
        category.Active.Should().BeTrue();
    }

    [Fact]
    public void ConstructorWithParentIdSetsParentId()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var category = new Category(id, "DRINKS", "Drinks", parentId, 0);

        category.ParentId.Should().Be(parentId);
    }

    [Fact]
    public void ConstructorInactiveCategorySetsActiveFalse()
    {
        var category = new Category(Guid.NewGuid(), "OFF", "Hidden", active: false);

        category.Active.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ConstructorEmptyCodeThrowsArgumentException(string? code)
    {
        var act = () => new Category(Guid.NewGuid(), code!, "Name");

        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ConstructorEmptyNameThrowsArgumentException(string? name)
    {
        var act = () => new Category(Guid.NewGuid(), "CODE", name!);

        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(name));
    }
}

public class TaxProfileTests
{
    [Fact]
    public void ConstructorValidParametersCreatesTaxProfile()
    {
        var id = Guid.NewGuid();
        var taxProfile = new TaxProfile(id, "VAT20", "KDV 20%", 20m);

        taxProfile.Id.Should().Be(id);
        taxProfile.Code.Should().Be("VAT20");
        taxProfile.Name.Should().Be("KDV 20%");
        taxProfile.VatRate.Should().Be(20m);
        taxProfile.Active.Should().BeTrue();
    }

    [Fact]
    public void ConstructorInactiveTaxProfileSetsActiveFalse()
    {
        var taxProfile = new TaxProfile(Guid.NewGuid(), "OFF", "Hidden", 0m, active: false);

        taxProfile.Active.Should().BeFalse();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void ConstructorInvalidVatRateThrowsArgumentOutOfRangeException(decimal vatRate)
    {
        var act = () => new TaxProfile(Guid.NewGuid(), "CODE", "Name", vatRate);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(nameof(vatRate));
    }

    [Fact]
    public void ConstructorVatRateZeroIsValid()
    {
        var taxProfile = new TaxProfile(Guid.NewGuid(), "TAX0", "No Tax", 0m);
        taxProfile.VatRate.Should().Be(0m);
    }

    [Fact]
    public void ConstructorVatRateHundredIsValid()
    {
        var taxProfile = new TaxProfile(Guid.NewGuid(), "TAX100", "Full Tax", 100m);
        taxProfile.VatRate.Should().Be(100m);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ConstructorEmptyCodeThrowsArgumentException(string? code)
    {
        var act = () => new TaxProfile(Guid.NewGuid(), code!, "Name", 10m);

        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ConstructorEmptyNameThrowsArgumentException(string? name)
    {
        var act = () => new TaxProfile(Guid.NewGuid(), "CODE", name!, 10m);

        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(name));
    }
}

public class ModifierGroupTests
{
    [Fact]
    public void ConstructorValidParametersCreatesModifierGroup()
    {
        var id = Guid.NewGuid();
        var group = new ModifierGroup(id, "TOPPINGS", "Toppings", SelectionType.SelectMany, 0, 3);

        group.Id.Should().Be(id);
        group.Code.Should().Be("TOPPINGS");
        group.Name.Should().Be("Toppings");
        group.SelectionType.Should().Be(SelectionType.SelectMany);
        group.MinSelections.Should().Be(0);
        group.MaxSelections.Should().Be(3);
        group.Active.Should().BeTrue();
    }

    [Fact]
    public void ConstructorDefaultsMatchPdfDefaults()
    {
        var group = new ModifierGroup(Guid.NewGuid(), "SIZES", "Sizes", SelectionType.SelectOne);

        group.MinSelections.Should().Be(0);
        group.MaxSelections.Should().Be(1);
    }

    [Fact]
    public void ConstructorInactiveGroupSetsActiveFalse()
    {
        var group = new ModifierGroup(Guid.NewGuid(), "OFF", "Hidden", SelectionType.SelectOne, active: false);

        group.Active.Should().BeFalse();
    }

    [Fact]
    public void ConstructorNegativeMinSelectionsThrowsArgumentOutOfRangeException()
    {
        var act = () => new ModifierGroup(Guid.NewGuid(), "CODE", "Name", SelectionType.SelectOne, -1, 1);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("minSelections");
    }

    [Fact]
    public void ConstructorMaxSelectionsLessThanMinSelectionsThrowsArgumentOutOfRangeException()
    {
        var act = () => new ModifierGroup(Guid.NewGuid(), "CODE", "Name", SelectionType.SelectOne, 2, 1);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxSelections");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ConstructorEmptyCodeThrowsArgumentException(string? code)
    {
        var act = () => new ModifierGroup(Guid.NewGuid(), code!, "Name", SelectionType.SelectOne);

        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ConstructorEmptyNameThrowsArgumentException(string? name)
    {
        var act = () => new ModifierGroup(Guid.NewGuid(), "CODE", name!, SelectionType.SelectOne);

        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(name));
    }
}

public class ModifierTests
{
    [Fact]
    public void ConstructorValidParametersCreatesModifier()
    {
        var id = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var modifier = new Modifier(id, groupId, "EXTRA_CHEESE", "Extra Cheese", 2.50m);

        modifier.Id.Should().Be(id);
        modifier.ModifierGroupId.Should().Be(groupId);
        modifier.Code.Should().Be("EXTRA_CHEESE");
        modifier.Name.Should().Be("Extra Cheese");
        modifier.PriceDelta.Should().Be(2.50m);
        modifier.ProductId.Should().BeNull();
        modifier.Active.Should().BeTrue();
    }

    [Fact]
    public void ConstructorDefaultsMatchPdfDefaults()
    {
        var modifier = new Modifier(Guid.NewGuid(), Guid.NewGuid(), "CODE", "Name");

        modifier.PriceDelta.Should().Be(0m);
    }

    [Fact]
    public void ConstructorWithProductIdSetsProductId()
    {
        var productId = Guid.NewGuid();
        var modifier = new Modifier(Guid.NewGuid(), Guid.NewGuid(), "CODE", "Name", 1m, productId);

        modifier.ProductId.Should().Be(productId);
    }

    [Fact]
    public void ConstructorInactiveModifierSetsActiveFalse()
    {
        var modifier = new Modifier(Guid.NewGuid(), Guid.NewGuid(), "CODE", "Name", active: false);

        modifier.Active.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ConstructorEmptyCodeThrowsArgumentException(string? code)
    {
        var act = () => new Modifier(Guid.NewGuid(), Guid.NewGuid(), code!, "Name", 1m);

        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ConstructorEmptyNameThrowsArgumentException(string? name)
    {
        var act = () => new Modifier(Guid.NewGuid(), Guid.NewGuid(), "CODE", name!, 1m);

        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(name));
    }
}

public class ProductTests
{
    [Fact]
    public void ConstructorMenuItemProductValid()
    {
        var id = Guid.NewGuid();
        var product = new Product(id, "SKU001", "Burger", ProductType.MenuItem, StockMode.QuantityTracked);

        product.Id.Should().Be(id);
        product.Sku.Should().Be("SKU001");
        product.Name.Should().Be("Burger");
        product.ProductType.Should().Be(ProductType.MenuItem);
        product.StockMode.Should().Be(StockMode.QuantityTracked);
        product.CategoryId.Should().BeNull();
        product.TaxProfileId.Should().BeNull();
        product.Description.Should().BeNull();
        product.PrinterRoutePolicy.Should().BeNull();
        product.DisplayOrder.Should().Be(0);
        product.CurrentPrice.Should().BeNull();
        product.Active.Should().BeTrue();
    }

    [Fact]
    public void ConstructorAllOptionalFieldsSet()
    {
        var categoryId = Guid.NewGuid();
        var taxProfileId = Guid.NewGuid();
        var product = new Product(
            Guid.NewGuid(),
            "SKU002",
            "Menu Item",
            ProductType.MenuItem,
            StockMode.RecipeDerived,
            categoryId,
            taxProfileId,
            "A description",
            "kitchen-1",
            3,
            12.50m,
            false);

        product.CategoryId.Should().Be(categoryId);
        product.TaxProfileId.Should().Be(taxProfileId);
        product.Description.Should().Be("A description");
        product.PrinterRoutePolicy.Should().Be("kitchen-1");
        product.DisplayOrder.Should().Be(3);
        product.CurrentPrice.Should().Be(12.50m);
        product.Active.Should().BeFalse();
    }

    [Fact]
    public void ConstructorAllCanonicalProductTypesAreAllowed()
    {
        foreach (var type in new[] { ProductType.MenuItem, ProductType.Modifier, ProductType.AddOn, ProductType.Packaging, ProductType.ServiceItem })
        {
            var product = new Product(Guid.NewGuid(), "SKU-" + (int)type, "Item", type, StockMode.Untracked);
            product.ProductType.Should().Be(type);
        }
    }

    [Fact]
    public void ConstructorAllCanonicalStockModesAreAllowed()
    {
        foreach (var mode in new[] { StockMode.Untracked, StockMode.QuantityTracked, StockMode.PortionTracked, StockMode.RecipeDerived })
        {
            var product = new Product(Guid.NewGuid(), "SKU-" + (int)mode, "Item", ProductType.MenuItem, mode);
            product.StockMode.Should().Be(mode);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ConstructorEmptySkuThrowsArgumentException(string? sku)
    {
        var act = () => new Product(Guid.NewGuid(), sku!, "Name", ProductType.MenuItem, StockMode.Untracked);

        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(sku));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ConstructorEmptyNameThrowsArgumentException(string? name)
    {
        var act = () => new Product(Guid.NewGuid(), "SKU001", name!, ProductType.MenuItem, StockMode.Untracked);

        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(name));
    }
}

public class ProductModifierGroupTests
{
    [Fact]
    public void ConstructorValidParametersCreatesLink()
    {
        var id = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var modifierGroupId = Guid.NewGuid();
        var link = new ProductModifierGroup(id, productId, modifierGroupId);

        link.Id.Should().Be(id);
        link.ProductId.Should().Be(productId);
        link.ModifierGroupId.Should().Be(modifierGroupId);
    }
}
