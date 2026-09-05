using System;
using ALKAROS.Recipes.Units;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Recipes.CostSnapshots.Tests;

public sealed class RecipeCostSnapshotDomainTests
{
    [Fact]
    public void OrderOfOperationsCalculatesEffectiveQuantityAndLineCostCorrectly()
    {
        // CORR:C9 and V0-DOM-010 decision:
        // Recipe calls for 200 g with waste_factor 0.05, stock tracked in kg:
        // 200 * 1.05 = 210 g, then 210 / 1000 = 0.21 kg
        // Unit cost 35.00 TRY/kg -> line cost 0.21 * 35.00 = 7.35 TRY
        var converter = new UnitConverter();
        var rawQty = 200m;
        var wasteFactor = 0.05m;
        var effNativeQty = Math.Round(rawQty * (1m + wasteFactor), 4, MidpointRounding.AwayFromZero);
        var stockQty = Math.Round(converter.Convert(effNativeQty, "g", "kg"), 4, MidpointRounding.AwayFromZero);

        effNativeQty.Should().Be(210m);
        stockQty.Should().Be(0.21m);

        var item = RecipeCostSnapshotItem.Create(
            snapshotId: Guid.NewGuid(),
            stockItemId: Guid.NewGuid(),
            rawQuantity: rawQty,
            wasteFactor: wasteFactor,
            nativeUnitCode: "g",
            stockQuantity: stockQty,
            stockUnitCode: "kg",
            unitCost: 35.00m);

        item.EffectiveNativeQuantity.Should().Be(210m);
        item.StockQuantity.Should().Be(0.21m);
        item.UnitCost.Should().Be(35.00m);
        item.LineCost.Should().Be(7.35m);
    }

    [Fact]
    public void SnapshotCalculatedCostSumsAllLineCosts()
    {
        var versionId = Guid.NewGuid();
        var snapshot = RecipeCostSnapshot.Create(versionId, new DateOnly(2026, 8, 10));

        var item1 = RecipeCostSnapshotItem.Create(
            snapshot.Id, Guid.NewGuid(), 200m, 0.05m, "g", 0.21m, "kg", 35.00m); // 7.35
        var item2 = RecipeCostSnapshotItem.Create(
            snapshot.Id, Guid.NewGuid(), 500m, 0.00m, "ml", 0.50m, "l", 20.00m); // 10.00

        snapshot.AddItem(item1);
        snapshot.AddItem(item2);

        snapshot.CalculatedCost.Should().Be(17.35m);
        snapshot.Items.Should().HaveCount(2);
        snapshot.CostBasisDate.Should().Be(new DateOnly(2026, 8, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void CreateItemWithInvalidRawQuantityThrowsInvalidCostSnapshotException(decimal invalidQty)
    {
        var act = () => RecipeCostSnapshotItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), invalidQty, 0.05m, "g", 1m, "kg", 10m);

        act.Should().Throw<InvalidCostSnapshotException>()
            .WithMessage("*greater than zero*");
    }

    [Fact]
    public void CreateItemWithNegativeWasteFactorThrowsInvalidCostSnapshotException()
    {
        var act = () => RecipeCostSnapshotItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 100m, -0.05m, "g", 0.1m, "kg", 10m);

        act.Should().Throw<InvalidCostSnapshotException>()
            .WithMessage("*Waste factor cannot be negative*");
    }

    [Fact]
    public void CreateItemWithNegativeUnitCostThrowsInvalidCostSnapshotException()
    {
        var act = () => RecipeCostSnapshotItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), 100m, 0.05m, "g", 0.1m, "kg", -10m);

        act.Should().Throw<InvalidCostSnapshotException>()
            .WithMessage("*Unit cost cannot be negative*");
    }
}
