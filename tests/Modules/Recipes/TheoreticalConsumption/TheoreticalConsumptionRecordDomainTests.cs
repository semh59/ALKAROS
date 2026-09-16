using FluentAssertions;
using Xunit;

namespace ALKAROS.Recipes.TheoreticalConsumption.Tests;

public sealed class TheoreticalConsumptionRecordDomainTests
{
    private static TheoreticalConsumptionRecord CreateValid(
        Guid? id = null, Guid? orderItemId = null, Guid? productId = null,
        Guid? recipeId = null, Guid? recipeVersionId = null, Guid? stockItemId = null,
        decimal quantity = 1m, string unitCode = "kg")
        => new(
            id ?? Guid.NewGuid(),
            orderItemId ?? Guid.NewGuid(),
            productId ?? Guid.NewGuid(),
            recipeId ?? Guid.NewGuid(),
            recipeVersionId ?? Guid.NewGuid(),
            stockItemId ?? Guid.NewGuid(),
            quantity,
            unitCode);

    [Fact]
    public void ZeroQuantityThrows()
    {
        var act = () => CreateValid(quantity: 0);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("quantity");
    }

    [Fact]
    public void NegativeQuantityThrows()
    {
        var act = () => CreateValid(quantity: -1);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("quantity");
    }

    [Fact]
    public void EmptyStockItemIdThrows()
    {
        var act = () => CreateValid(stockItemId: Guid.Empty);
        act.Should().Throw<ArgumentException>().WithParameterName("stockItemId");
    }

    [Fact]
    public void EmptyUnitCodeThrows()
    {
        var act = () => CreateValid(unitCode: "  ");
        act.Should().Throw<ArgumentException>().WithParameterName("unitCode");
    }

    [Fact]
    public void UnitCodeIsNormalizedToLowerInvariant()
    {
        var record = CreateValid(unitCode: "KG");
        record.UnitCode.Should().Be("kg");
    }
}
