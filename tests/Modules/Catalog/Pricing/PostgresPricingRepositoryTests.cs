using ALKAROS.Catalog.Pricing.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace ALKAROS.Catalog.Pricing.Tests;

/// <summary>
/// Integration tests that exercise the PostgreSQL pricing repository and the
/// 007-catalog-pricing migration constraints (overlap exclusion, check and
/// foreign key) against a real database created from 006 + 007.
/// </summary>
public sealed class PostgresPricingRepositoryTests : IClassFixture<PricingTestDatabase>
{
    private const string ExclusionViolation = "23P01";
    private const string CheckViolation = "23514";
    private const string ForeignKeyViolation = "23503";

    private static readonly DateTimeOffset Start = Ts(2026, 8, 1, 10, 0);
    private static readonly DateTimeOffset End = Ts(2026, 8, 1, 18, 0);

    private readonly PricingTestDatabase _database;
    private readonly PostgresPricingRepository _prices;

    public PostgresPricingRepositoryTests(PricingTestDatabase database)
    {
        _database = database;
        _prices = new PostgresPricingRepository(database.DataSource);
    }

    [Fact]
    public async Task RoundTripPersistsAllFields()
    {
        var productId = await InsertProductAsync("SKU-RT");
        var id = Guid.NewGuid();
        await _prices.AddAsync(new ProductPrice(id, productId, PriceType.SalePrice, 149.90m, Start, "TRY", End));

        var byId = await _prices.GetByIdAsync(id);
        Assert.NotNull(byId);
        Assert.Equal(id, byId.Id);
        Assert.Equal(productId, byId.ProductId);
        Assert.Equal(PriceType.SalePrice, byId.PriceType);
        Assert.Equal(149.90m, byId.Price);
        Assert.Equal("TRY", byId.CurrencyCode);
        Assert.Equal(Start, byId.EffectiveFrom);
        Assert.Equal(End, byId.EffectiveTo);

        var byProduct = await _prices.GetByProductAsync(productId);
        Assert.Contains(byProduct, p => p.Id == id);
    }

    [Fact]
    public async Task EffectiveLookupResolvesSinglePriceInsideClosedRange()
    {
        var productId = await InsertProductAsync("SKU-INSIDE");
        await _prices.AddAsync(new ProductPrice(Guid.NewGuid(), productId, PriceType.SalePrice, 120m, Start, effectiveTo: End));

        var atStart = await _prices.GetEffectivePriceAsync(productId, PriceType.SalePrice, "TRY", Start);
        var atMiddle = await _prices.GetEffectivePriceAsync(productId, PriceType.SalePrice, "TRY", Ts(2026, 8, 1, 14, 0));

        Assert.NotNull(atStart);
        Assert.Equal(120m, atStart.Price);
        Assert.Equal(Start, atStart.EffectiveFrom);
        Assert.NotNull(atMiddle);
        Assert.Equal(120m, atMiddle.Price);
    }

    [Fact]
    public async Task EffectiveLookupAtEndBoundaryReturnsNullForClosedRange()
    {
        var productId = await InsertProductAsync("SRU-BOUND");
        await _prices.AddAsync(new ProductPrice(Guid.NewGuid(), productId, PriceType.SalePrice, 90m, Start, effectiveTo: End));

        var atEnd = await _prices.GetEffectivePriceAsync(productId, PriceType.SalePrice, "TRY", End);

        Assert.Null(atEnd);
    }

    [Fact]
    public async Task OpenEndedRangeResolvesAnyLaterTimestamp()
    {
        var productId = await InsertProductAsync("SKU-OPEN");
        await _prices.AddAsync(new ProductPrice(Guid.NewGuid(), productId, PriceType.SalePrice, 200m, Start));

        var future = await _prices.GetEffectivePriceAsync(
            productId, PriceType.SalePrice, "TRY", Ts(2030, 1, 1, 0, 0));

        Assert.NotNull(future);
        Assert.Equal(200m, future.Price);
    }

    [Fact]
    public async Task AtMostOnePriceResolvesWhenPeriodsAreAdjacent()
    {
        var productId = await InsertProductAsync("SKU-ADJ");
        await _prices.AddAsync(new ProductPrice(Guid.NewGuid(), productId, PriceType.SalePrice, 100m, Start, effectiveTo: End));
        await _prices.AddAsync(new ProductPrice(Guid.NewGuid(), productId, PriceType.SalePrice, 130m, End));

        var atJustBefore = await _prices.GetEffectivePriceAsync(productId, PriceType.SalePrice, "TRY", Ts(2026, 8, 1, 17, 59));
        var atBoundary = await _prices.GetEffectivePriceAsync(productId, PriceType.SalePrice, "TRY", End);
        var afterBoundary = await _prices.GetEffectivePriceAsync(productId, PriceType.SalePrice, "TRY", Ts(2026, 8, 2, 0, 0));

        Assert.NotNull(atJustBefore);
        Assert.Equal(100m, atJustBefore.Price);
        Assert.NotNull(atBoundary);
        Assert.Equal(130m, atBoundary.Price);
        Assert.NotNull(afterBoundary);
        Assert.Equal(130m, afterBoundary.Price);
    }

    [Fact]
    public async Task OverlapIsRejectedByExclusionConstraint()
    {
        var productId = await InsertProductAsync("SKU-OVL");
        await _prices.AddAsync(new ProductPrice(Guid.NewGuid(), productId, PriceType.SalePrice, 100m, Start, effectiveTo: End));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _prices.AddAsync(new ProductPrice(Guid.NewGuid(), productId, PriceType.SalePrice, 95m, Ts(2026, 8, 1, 12, 0))));

        Assert.Equal(ExclusionViolation, ex.SqlState);
    }

    [Fact]
    public async Task IdenticalRangeInAnotherCurrencyIsAllowed()
    {
        var productId = await InsertProductAsync("SKU-CCY");
        await _prices.AddAsync(new ProductPrice(Guid.NewGuid(), productId, PriceType.SalePrice, 100m, Start, "TRY", effectiveTo: End));
        await _prices.AddAsync(new ProductPrice(Guid.NewGuid(), productId, PriceType.SalePrice, 30.00m, Start, "EUR", effectiveTo: End));

        var eur = await _prices.GetEffectivePriceAsync(productId, PriceType.SalePrice, "EUR", Ts(2026, 8, 1, 12, 0));

        Assert.NotNull(eur);
        Assert.Equal(30.00m, eur.Price);
        Assert.Equal("EUR", eur.CurrencyCode);
    }

    [Fact]
    public async Task UpdatePersistsPriceAndBounds()
    {
        var productId = await InsertProductAsync("SKU-UPD");
        var id = Guid.NewGuid();
        await _prices.AddAsync(new ProductPrice(id, productId, PriceType.SalePrice, 100m, Start, effectiveTo: End));

        var updated = new ProductPrice(id, productId, PriceType.SalePrice, 155m, Ts(2026, 8, 2, 0, 0));
        await _prices.UpdateAsync(updated);

        var byId = await _prices.GetByIdAsync(id);
        Assert.NotNull(byId);
        Assert.Equal(155m, byId.Price);
        Assert.Equal(updated.EffectiveFrom, byId.EffectiveFrom);
        Assert.Null(byId.EffectiveTo);
    }

    [Fact]
    public async Task UpdateOfMissingRowThrowsInvalidOperationException()
    {
        var act = () => _prices.UpdateAsync(new ProductPrice(
            Guid.NewGuid(), Guid.NewGuid(), PriceType.SalePrice, 100m, Start));

        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task DeleteRemovesPriceRecord()
    {
        var productId = await InsertProductAsync("SKU-DEL");
        var id = Guid.NewGuid();
        await _prices.AddAsync(new ProductPrice(id, productId, PriceType.SalePrice, 100m, Start));

        await _prices.DeleteAsync(id);

        Assert.Null(await _prices.GetByIdAsync(id));
        var byProduct = await _prices.GetByProductAsync(productId);
        Assert.DoesNotContain(byProduct, p => p.Id == id);
    }

    [Fact]
    public async Task PriceForUnknownProductIsRejectedByForeignKeyConstraint()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _prices.AddAsync(new ProductPrice(Guid.NewGuid(), Guid.NewGuid(), PriceType.SalePrice, 100m, Start)));

        Assert.Equal(ForeignKeyViolation, ex.SqlState);
    }

    [Fact]
    public async Task UnknownPriceTypeIsRejectedByCheckConstraint()
    {
        var productId = await InsertProductAsync("SKU-BADTYPE");
        var ex = await Assert.ThrowsAsync<PostgresException>(() => _database.ExecuteAsync(
            "INSERT INTO catalog.product_prices (product_price_id, product_id, price_type, price, effective_from) " +
            "VALUES (@id, @product_id, @price_type, @price, @effective_from);",
            ("id", Guid.NewGuid()),
            ("product_id", productId),
            ("price_type", 9),
            ("price", 100m),
            ("effective_from", Start)));

        Assert.Equal(CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task DefaultCurrencyIsTry()
    {
        var productId = await InsertProductAsync("SKU-DEFCCY");
        await _database.ExecuteAsync(
            "INSERT INTO catalog.product_prices (product_price_id, product_id, price_type, price, effective_from) " +
            "VALUES (@id, @product_id, 1, @price, @effective_from);",
            ("id", Guid.NewGuid()),
            ("product_id", productId),
            ("price", 100m),
            ("effective_from", Start));

        var byProduct = await _prices.GetByProductAsync(productId);

        var stored = Assert.Single(byProduct);
        Assert.Equal("TRY", stored.CurrencyCode);
    }

    private async Task<Guid> InsertProductAsync(string sku)
    {
        var productId = Guid.NewGuid();
        await _database.ExecuteAsync(
            "INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode) " +
            "VALUES (@product_id, @sku, @name, 1, 1);",
            ("product_id", productId), ("sku", sku), ("name", sku));
        return productId;
    }

    private static DateTimeOffset Ts(int year, int month, int day, int hour, int minute)
        => new(year, month, day, hour, minute, 0, TimeSpan.Zero);
}

/// <summary>
/// Verifies the 007 rollback script drops only the pricing table while the
/// 006 catalog schema remains intact. Uses its own database so it cannot
/// invalidate the schema shared by PostgresPricingRepositoryTests.
/// </summary>
public sealed class PricingRollbackScriptTests : IClassFixture<PricingTestDatabase>
{
    private readonly PricingTestDatabase _database;

    public PricingRollbackScriptTests(PricingTestDatabase database)
    {
        _database = database;
    }

    [Fact]
    public async Task RollbackScriptDropsOnlyProductPricesTable()
    {
        var downSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "007-catalog-pricing.down.sql"));
        await _database.ExecuteAsync(downSql);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _database.ExecuteAsync("SELECT count(*) FROM catalog.product_prices;"));
        Assert.Equal("42P01", ex.SqlState);

        var remaining = await _database.ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'catalog';");
        Assert.Equal(6, remaining);
    }
}