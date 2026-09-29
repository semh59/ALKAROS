using Xunit;

namespace ALKAROS.Invoicing.Generation.Tests;

/// <summary>V14-INV-002: the invoice arithmetic - proportional split to the kurus and tax-inclusive grouping.</summary>
public sealed class InvoiceTaxCalculatorTests
{
    [Fact]
    public void AChargeIsSplitByEachRatesShareOfTheBillAndThePartsAddUpExactly()
    {
        var split = InvoiceTaxCalculator.SplitByTaxRate(100m, [new(10m, 110m), new(20m, 120m)])!;

        // 100 x 120/230 = 52.1739..., 100 x 110/230 = 47.8260...: the leftover kurus goes to the larger remainder.
        Assert.Equal(52.17m, split[20m]);
        Assert.Equal(47.83m, split[10m]);
        Assert.Equal(100m, split.Values.Sum());
    }

    [Fact]
    public void AnEvenTieGivesTheLeftoverKurusToTheHigherRate()
    {
        var split = InvoiceTaxCalculator.SplitByTaxRate(0.01m, [new(10m, 50m), new(20m, 50m)])!;

        Assert.Equal(0.01m, split[20m]);
        Assert.False(split.ContainsKey(10m));
    }

    [Fact]
    public void AFullChargeReproducesTheBillsOwnRateTotals()
    {
        var split = InvoiceTaxCalculator.SplitByTaxRate(
            333.33m, [new(1m, 111.11m), new(10m, 111.11m), new(20m, 111.11m)])!;

        Assert.Equal(111.11m, split[1m]);
        Assert.Equal(111.11m, split[10m]);
        Assert.Equal(111.11m, split[20m]);
    }

    [Fact]
    public void WeightsAtTheSameRateAddUpAndAFullyDiscountedRateTakesNoShare()
    {
        var split = InvoiceTaxCalculator.SplitByTaxRate(
            90m, [new(10m, 60m), new(10m, 30m), new(20m, 40m), new(20m, -40m)])!;

        Assert.Equal(90m, split[10m]);
        Assert.Single(split);
    }

    [Fact]
    public void ABillWithNoPositiveRateCannotBeSplit()
    {
        Assert.Null(InvoiceTaxCalculator.SplitByTaxRate(10m, []));
        Assert.Null(InvoiceTaxCalculator.SplitByTaxRate(10m, [new(10m, 0m), new(20m, -5m)]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void OnlyAPositiveAmountIsSplit(int amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => InvoiceTaxCalculator.SplitByTaxRate(amount, [new(10m, 10m)]));
    }

    [Fact]
    public void GroupsSeparateTheTaxFromTheTaxInclusiveGrossAndKeepNetPlusTaxEqualToGross()
    {
        var groups = InvoiceTaxCalculator.Groups(new Dictionary<decimal, decimal>
        {
            [10m] = 110m,
            [20m] = 52.17m,
            [1m] = 0.05m,
        });

        Assert.Equal([20m, 10m, 1m], groups.Select(group => group.TaxRate));
        Assert.Equal(new InvoiceTaxGroup(20m, 43.47m, 8.70m, 52.17m), groups[0]); // 52.17 x 20/120 = 8.695 -> 8.70
        Assert.Equal(new InvoiceTaxGroup(10m, 100m, 10m, 110m), groups[1]);
        Assert.Equal(new InvoiceTaxGroup(1m, 0.05m, 0m, 0.05m), groups[2]);
        Assert.All(groups, group => Assert.Equal(group.GrossAmount, group.NetAmount + group.TaxAmount));
    }

    [Fact]
    public void MoneyRoundsHalfAwayFromZero()
    {
        Assert.Equal(0.13m, InvoiceTaxCalculator.RoundMoney(0.125m));
        Assert.Equal(0.12m, InvoiceTaxCalculator.RoundMoney(0.1249m));
    }
}
