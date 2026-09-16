using FluentAssertions;
using Xunit;

namespace ALKAROS.Recipes.CatalogMapping.Tests;

public sealed class ProductRecipeMappingDomainTests
{
    [Fact]
    public void ConstructorWithEmptyProductIdThrows()
    {
        var act = () => new ProductRecipeMapping(Guid.Empty, Guid.NewGuid());
        act.Should().Throw<ArgumentException>().WithParameterName("productId");
    }

    [Fact]
    public void ConstructorWithEmptyRecipeIdThrows()
    {
        var act = () => new ProductRecipeMapping(Guid.NewGuid(), Guid.Empty);
        act.Should().Throw<ArgumentException>().WithParameterName("recipeId");
    }

    [Fact]
    public void ConstructorTrimsNotes()
    {
        var mapping = new ProductRecipeMapping(Guid.NewGuid(), Guid.NewGuid(), notes: "  padded  ");
        mapping.Notes.Should().Be("padded");
    }

    [Fact]
    public void UpdateWithEmptyRecipeIdThrows()
    {
        var mapping = new ProductRecipeMapping(Guid.NewGuid(), Guid.NewGuid());
        var act = () => mapping.Update(Guid.Empty, isActive: false, notes: null);
        act.Should().Throw<ArgumentException>().WithParameterName("recipeId");
    }

    [Fact]
    public void UpdateReplacesRecipeActiveAndNotes()
    {
        var mapping = new ProductRecipeMapping(Guid.NewGuid(), Guid.NewGuid());
        var newRecipeId = Guid.NewGuid();

        mapping.Update(newRecipeId, isActive: false, notes: "  updated  ");

        mapping.RecipeId.Should().Be(newRecipeId);
        mapping.IsActive.Should().BeFalse();
        mapping.Notes.Should().Be("updated");
    }
}
