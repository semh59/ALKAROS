using FluentAssertions;
using Xunit;

namespace ALKAROS.Measurements.Tests;

public sealed class DimensionSafeUnitTests
{
    private readonly UnitConverter _converter = new();

    [Fact]
    public void ConvertKgToGAndGToKgIsReversibleWithinTolerance()
    {
        const decimal originalKg = 2.75m;
        var grams = _converter.Convert(originalKg, "kg", "g");
        grams.Should().Be(2750.0m);

        var roundtripKg = _converter.Convert(grams, "g", "kg");
        Math.Abs(roundtripKg - originalKg).Should().BeLessThanOrEqualTo(UnitConverter.InvertibilityTolerance);
        roundtripKg.Should().Be(originalKg);
    }

    [Fact]
    public void ConvertLitreToMlAndMlToLitreIsReversibleWithinTolerance()
    {
        const decimal originalLitre = 1.450m;
        var ml = _converter.Convert(originalLitre, "l", "ml");
        ml.Should().Be(1450.0m);

        var roundtripLitre = _converter.Convert(ml, "ml", "l");
        Math.Abs(roundtripLitre - originalLitre).Should().BeLessThanOrEqualTo(UnitConverter.InvertibilityTolerance);
        roundtripLitre.Should().Be(originalLitre);
    }

    [Fact]
    public void ConvertCentiliterToMilliliterAndLitreIsAccurate()
    {
        const decimal cl = 25m;
        var ml = _converter.Convert(cl, "cl", "ml");
        ml.Should().Be(250.0m);

        var l = _converter.Convert(cl, "cl", "l");
        l.Should().Be(0.25m);

        var roundtripCl = _converter.Convert(l, "l", "cl");
        roundtripCl.Should().Be(cl);
    }

    [Fact]
    public void ConvertMilligramToGramToKilogramIsAccurate()
    {
        const decimal mg = 500_000m;
        var g = _converter.Convert(mg, "mg", "g");
        g.Should().Be(500.0m);

        var kg = _converter.Convert(mg, "mg", "kg");
        kg.Should().Be(0.5m);

        var roundtripMg = _converter.Convert(kg, "kg", "mg");
        roundtripMg.Should().Be(mg);
    }

    [Fact]
    public void ConvertCrossDimensionKgToLitreWithoutBridgeThrowsIncompatibleDimensionException()
    {
        var act = () => _converter.Convert(1.5m, "kg", "l");

        act.Should().Throw<IncompatibleUnitDimensionException>()
            .WithMessage("*cross-dimension conversion without bridge conversion rule is prohibited*");
    }

    [Fact]
    public void ConvertCrossDimensionWithRegisteredBridgeSucceeds()
    {
        // 1 liter of olive oil = 0.92 kg (density 0.92)
        _converter.RegisterConversion("l", "kg", 0.92m);

        var kg = _converter.Convert(2.0m, "l", "kg");
        kg.Should().Be(1.84m);

        var l = _converter.Convert(1.84m, "kg", "l");
        Math.Abs(l - 2.0m).Should().BeLessThanOrEqualTo(UnitConverter.InvertibilityTolerance);
    }

    [Fact]
    public void RegisterConversionContradictoryFactorThrowsContradictoryConversionException()
    {
        _converter.RegisterConversion("piece", "pack", 0.1m);

        // Contradictory reverse: says 1 pack = 5 pieces instead of 10
        var act = () => _converter.RegisterConversion("pack", "piece", 5.0m);

        act.Should().Throw<ContradictoryUnitConversionException>()
            .WithMessage("*Contradictory conversion detected*");
    }

    [Fact]
    public void RegisterConversionZeroOrNegativeFactorThrowsInvalidFactorException()
    {
        var actZero = () => _converter.RegisterConversion("kg", "g", 0m);
        actZero.Should().Throw<InvalidUnitConversionFactorException>();

        var actNegative = () => _converter.RegisterConversion("kg", "g", -1.5m);
        actNegative.Should().Throw<InvalidUnitConversionFactorException>();
    }

    [Fact]
    public void ConvertNegativeQuantityThrowsInvalidQuantityException()
    {
        var act = () => _converter.Convert(-10m, "kg", "g");
        act.Should().Throw<InvalidUnitQuantityException>();
    }

    [Fact]
    public void ConvertSameUnitReturnsExactQuantity()
    {
        const decimal qty = 42.1234m;
        _converter.Convert(qty, "kg", "kg").Should().Be(qty);
        _converter.Convert(qty, "ml", "ml").Should().Be(qty);
        _converter.Convert(qty, "piece", "piece").Should().Be(qty);
    }

    [Fact]
    public void ConvertUnknownUnitThrowsUnknownUnitException()
    {
        var act = () => _converter.Convert(10m, "unknown_xyz", "kg");
        act.Should().Throw<UnknownUnitException>();
    }

    [Fact]
    public void ConcurrentConversionsUnderParallelThreadsAreThreadSafeAndDeterministic()
    {
        Parallel.For(0, 1000, i =>
        {
            var val = (decimal)i + 0.5m;
            var g = _converter.Convert(val, "kg", "g");
            g.Should().Be(val * 1000m);

            var roundtrip = _converter.Convert(g, "g", "kg");
            roundtrip.Should().Be(val);
        });
    }
}
