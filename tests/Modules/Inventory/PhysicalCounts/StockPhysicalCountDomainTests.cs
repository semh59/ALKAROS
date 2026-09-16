using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.PhysicalCounts.Tests;

public sealed class StockPhysicalCountDomainTests
{
    private static StockPhysicalCount CreateValid(
        decimal countedQuantity = 10m, decimal previousOnHandQuantity = 8m, Guid? countedByUserId = null)
        => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            countedQuantity, previousOnHandQuantity, countedByUserId ?? Guid.NewGuid(), null, null);

    [Fact]
    public void NegativeCountedQuantityThrows()
    {
        var act = () => CreateValid(countedQuantity: -1m);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("countedQuantity");
    }

    [Fact]
    public void EmptyCountedByUserIdThrows()
    {
        var act = () => CreateValid(countedByUserId: Guid.Empty);
        act.Should().Throw<ArgumentException>().WithParameterName("countedByUserId");
    }

    [Fact]
    public void DeltaIsCountedMinusPrevious()
    {
        var count = CreateValid(countedQuantity: 10m, previousOnHandQuantity: 8m);
        count.Delta.Should().Be(2m);
    }

    [Fact]
    public void DeltaCanBeNegative()
    {
        var count = CreateValid(countedQuantity: 3m, previousOnHandQuantity: 8m);
        count.Delta.Should().Be(-5m);
    }

    [Fact]
    public void ZeroCountedQuantityIsAllowed()
    {
        var act = () => CreateValid(countedQuantity: 0m);
        act.Should().NotThrow();
    }
}
