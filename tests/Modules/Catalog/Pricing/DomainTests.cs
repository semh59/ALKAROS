namespace ALKAROS.Catalog.Pricing.Tests;

using ALKAROS.Catalog.Pricing;
using FluentAssertions;
using Xunit;

public class ProductPriceTests
{
    [Fact]
    public void ConstructorValidParametersCreatesProductPrice()
    {
        var id = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
        var price = new ProductPrice(id, productId, PriceType.SalePrice, 149.90m, from);

        price.Id.Should().Be(id);
        price.ProductId.Should().Be(productId);
        price.PriceType.Should().Be(PriceType.SalePrice);
        price.Price.Should().Be(149.90m);
        price.CurrencyCode.Should().Be("TRY");
        price.EffectiveFrom.Should().Be(from);
        price.EffectiveTo.Should().BeNull();
    }

    [Fact]
    public void ConstructorWithoutEffectiveToIsOpenEnded()
    {
        var price = new ProductPrice(Guid.NewGuid(), Guid.NewGuid(), PriceType.SalePrice, 10m,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));

        price.EffectiveTo.Should().BeNull();
    }

    [Fact]
    public void ConstructorWithEffectiveToSetsBounds()
    {
        var from = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero);
        var price = new ProductPrice(Guid.NewGuid(), Guid.NewGuid(), PriceType.SalePrice, 10m, from, "EUR", to);

        price.EffectiveTo.Should().Be(to);
        price.CurrencyCode.Should().Be("EUR");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ConstructorInvalidCurrencyCodeThrowsArgumentException(string? currency)
    {
        var act = () => new ProductPrice(Guid.NewGuid(), Guid.NewGuid(), PriceType.SalePrice, 10m,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), currency!);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("currencyCode");
    }

    [Theory]
    [InlineData("TR")]
    [InlineData("TURK")]
    public void ConstructorNonThreeLetterCurrencyCodeThrowsArgumentException(string currency)
    {
        var act = () => new ProductPrice(Guid.NewGuid(), Guid.NewGuid(), PriceType.SalePrice, 10m,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), currency);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("currencyCode");
    }

    [Fact]
    public void ConstructorNegativePriceThrowsArgumentOutOfRangeException()
    {
        var act = () => new ProductPrice(Guid.NewGuid(), Guid.NewGuid(), PriceType.SalePrice, -0.01m,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("price");
    }

    [Fact]
    public void ConstructorZeroPriceIsValid()
    {
        var price = new ProductPrice(Guid.NewGuid(), Guid.NewGuid(), PriceType.SalePrice, 0m,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));

        price.Price.Should().Be(0m);
    }

    [Fact]
    public void ConstructorEffectiveToBeforeOrEqualToFromThrowsArgumentOutOfRangeException()
    {
        var from = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero);
        var same = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero);
        var before = new DateTimeOffset(2026, 8, 9, 0, 0, 0, TimeSpan.Zero);

        var actEqual = () => new ProductPrice(Guid.NewGuid(), Guid.NewGuid(), PriceType.SalePrice, 10m, from, "TRY", same);
        var actBefore = () => new ProductPrice(Guid.NewGuid(), Guid.NewGuid(), PriceType.SalePrice, 10m, from, "TRY", before);

        actEqual.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("effectiveTo");
        actBefore.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("effectiveTo");
    }

    [Fact]
    public void ConstructorEmptyIdThrowsArgumentException()
    {
        var act = () => new ProductPrice(Guid.Empty, Guid.NewGuid(), PriceType.SalePrice, 10m,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));

        act.Should().Throw<ArgumentException>()
            .WithParameterName("id");
    }

    [Fact]
    public void ConstructorEmptyProductIdThrowsArgumentException()
    {
        var act = () => new ProductPrice(Guid.NewGuid(), Guid.Empty, PriceType.SalePrice, 10m,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));

        act.Should().Throw<ArgumentException>()
            .WithParameterName("productId");
    }
}