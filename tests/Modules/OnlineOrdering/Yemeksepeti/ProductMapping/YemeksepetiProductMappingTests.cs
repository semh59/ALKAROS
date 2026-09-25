using FluentAssertions;
using Xunit;

namespace ALKAROS.OnlineOrdering.Yemeksepeti.ProductMapping.Tests;

public sealed class YemeksepetiProductMappingTests : IClassFixture<ProductMappingTestDatabase>
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly ProductMappingTestDatabase _db;
    private readonly PostgresYemeksepetiProductMappingService _service;
    private readonly Guid _actor = Guid.NewGuid();

    public YemeksepetiProductMappingTests(ProductMappingTestDatabase db)
    {
        _db = db;
        _service = db.CreateService();
    }

    private static string Sku() => "ys-" + Guid.NewGuid().ToString("N")[..10];

    [Fact]
    public async Task AMappedSkuResolvesToItsActiveProduct()
    {
        var product = await _db.SeedProductAsync();
        var sku = Sku();
        var mapping = await _service.MapAsync(sku, product, T0, _actor);

        var resolution = await _service.ResolveAsync(sku, T0.AddHours(1));

        resolution.Outcome.Should().Be(ProductMappingResolutionOutcome.Resolved);
        resolution.ProductId.Should().Be(product);
        resolution.MappingId.Should().Be(mapping.MappingId);
    }

    [Fact]
    public async Task AnUnknownSkuOrAMomentBeforeTheMappingIsUnmapped()
    {
        var product = await _db.SeedProductAsync();
        var sku = Sku();
        await _service.MapAsync(sku, product, T0, _actor);

        (await _service.ResolveAsync(Sku(), T0.AddHours(1))).Outcome.Should().Be(ProductMappingResolutionOutcome.Unmapped);
        var early = await _service.ResolveAsync(sku, T0.AddSeconds(-1));
        early.Outcome.Should().Be(ProductMappingResolutionOutcome.Unmapped);
        early.IsResolved.Should().BeFalse();
        early.ProductId.Should().BeNull();
    }

    [Fact]
    public async Task RemappingASkuEndsTheOldMappingAndKeepsItsHistory()
    {
        var first = await _db.SeedProductAsync();
        var second = await _db.SeedProductAsync();
        var sku = Sku();
        await _service.MapAsync(sku, first, T0, _actor);
        await _service.MapAsync(sku, second, T0.AddDays(1), _actor);

        (await _service.ResolveAsync(sku, T0.AddHours(12))).ProductId.Should().Be(first);
        (await _service.ResolveAsync(sku, T0.AddDays(1))).ProductId.Should().Be(second);
        (await _db.CountMappingsAsync(sku)).Should().Be(2);
    }

    [Fact]
    public async Task HistoryIsNeverRewrittenBackwards()
    {
        var first = await _db.SeedProductAsync();
        var second = await _db.SeedProductAsync();
        var sku = Sku();
        await _service.MapAsync(sku, first, T0, _actor);

        var act = () => _service.MapAsync(sku, second, T0, _actor);

        (await act.Should().ThrowAsync<ProductMappingRejectedException>())
            .Which.Rejection.Should().Be(ProductMappingRejection.LaterMappingExists);
        (await _service.ResolveAsync(sku, T0.AddHours(1))).ProductId.Should().Be(first);
    }

    [Fact]
    public async Task MappingTheSameSkuToTheSameProductAgainIsAReplay()
    {
        var product = await _db.SeedProductAsync();
        var sku = Sku();
        var first = await _service.MapAsync(sku, product, T0, _actor);

        var again = await _service.MapAsync(" " + sku + " ", product, T0.AddDays(3), _actor);

        again.MappingId.Should().Be(first.MappingId);
        (await _db.CountMappingsAsync(sku)).Should().Be(1);
    }

    [Fact]
    public async Task AProductIsPublishedUnderOneSkuAtATime()
    {
        var product = await _db.SeedProductAsync();
        await _service.MapAsync(Sku(), product, T0, _actor);

        var act = () => _service.MapAsync(Sku(), product, T0.AddDays(1), _actor);

        (await act.Should().ThrowAsync<ProductMappingRejectedException>())
            .Which.Rejection.Should().Be(ProductMappingRejection.ProductMappedToAnotherSku);
    }

    [Fact]
    public async Task OnlyAnExistingActiveProductCanBeMapped()
    {
        var inactive = await _db.SeedProductAsync(active: false);

        var missing = () => _service.MapAsync(Sku(), Guid.NewGuid(), T0, _actor);
        var deactivated = () => _service.MapAsync(Sku(), inactive, T0, _actor);

        (await missing.Should().ThrowAsync<ProductMappingRejectedException>())
            .Which.Rejection.Should().Be(ProductMappingRejection.ProductNotFound);
        (await deactivated.Should().ThrowAsync<ProductMappingRejectedException>())
            .Which.Rejection.Should().Be(ProductMappingRejection.ProductInactive);
    }

    [Fact]
    public async Task AProductDeactivatedAfterMappingNoLongerResolves()
    {
        var product = await _db.SeedProductAsync();
        var sku = Sku();
        await _service.MapAsync(sku, product, T0, _actor);
        await _db.SetProductActiveAsync(product, false);

        var resolution = await _service.ResolveAsync(sku, T0.AddHours(1));

        resolution.Outcome.Should().Be(ProductMappingResolutionOutcome.ProductInactive);
        resolution.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task AProductThatNeedsAModifierChoiceCannotBeSoldThroughTheChannel()
    {
        var needsChoice = await _db.SeedProductAsync();
        await _db.AttachModifierGroupAsync(needsChoice, minSelections: 1);
        var optionalOnly = await _db.SeedProductAsync();
        await _db.AttachModifierGroupAsync(optionalOnly, minSelections: 0);

        var act = () => _service.MapAsync(Sku(), needsChoice, T0, _actor);

        (await act.Should().ThrowAsync<ProductMappingRejectedException>())
            .Which.Rejection.Should().Be(ProductMappingRejection.ProductRequiresModifierChoice);
        var optionalSku = Sku();
        await _service.MapAsync(optionalSku, optionalOnly, T0, _actor);
        (await _service.ResolveAsync(optionalSku, T0.AddHours(1))).IsResolved.Should().BeTrue();
    }

    [Fact]
    public async Task AMandatoryChoiceAddedLaterStopsResolution()
    {
        var product = await _db.SeedProductAsync();
        var sku = Sku();
        await _service.MapAsync(sku, product, T0, _actor);
        await _db.AttachModifierGroupAsync(product, minSelections: 1);

        (await _service.ResolveAsync(sku, T0.AddHours(1))).Outcome
            .Should().Be(ProductMappingResolutionOutcome.ProductRequiresModifierChoice);
    }

    [Fact]
    public async Task OverlappingHistoryIsAmbiguousAndResolvesToNothing()
    {
        var first = await _db.SeedProductAsync();
        var second = await _db.SeedProductAsync();
        var sku = Sku();
        await _db.InsertRawMappingAsync(sku, first, T0, T0.AddDays(2));
        await _db.InsertRawMappingAsync(sku, second, T0.AddDays(1), T0.AddDays(3));

        var resolution = await _service.ResolveAsync(sku, T0.AddDays(1).AddHours(1));

        resolution.Outcome.Should().Be(ProductMappingResolutionOutcome.Ambiguous);
        resolution.ProductId.Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentMappingsOfOneSkuLeaveExactlyOneWinner()
    {
        var products = new List<Guid>();
        for (var i = 0; i < 5; i++)
            products.Add(await _db.SeedProductAsync());
        var sku = Sku();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var racers = products.Select(async product =>
        {
            await start.Task;
            try
            {
                await _service.MapAsync(sku, product, T0, _actor);
                return true;
            }
            catch (ProductMappingRejectedException rejected) when (rejected.Rejection == ProductMappingRejection.LaterMappingExists)
            {
                return false;
            }
        }).ToList();
        start.SetResult();

        (await Task.WhenAll(racers)).Count(won => won).Should().Be(1);
        (await _db.CountMappingsAsync(sku)).Should().Be(1);
    }

    [Fact]
    public async Task TheDatabaseItselfRefusesTwoOpenMappingsForOneSku()
    {
        var first = await _db.SeedProductAsync();
        var second = await _db.SeedProductAsync();
        var sku = Sku();
        await _db.InsertRawMappingAsync(sku, first, T0, null);

        var act = () => _db.InsertRawMappingAsync(sku, second, T0.AddDays(1), null);

        await act.Should().ThrowAsync<Npgsql.PostgresException>().Where(e => e.SqlState == "23505");
    }

    [Fact]
    public async Task TheMigrationRollsBackAndReapplies()
    {
        await _db.RunSqlFileAsync("144-yemeksepeti-product-mappings.down.sql");
        await _db.RunSqlFileAsync("144-yemeksepeti-product-mappings.up.sql");
        var product = await _db.SeedProductAsync();
        var sku = Sku();

        await _service.MapAsync(sku, product, T0, _actor);

        (await _service.ResolveAsync(sku, T0.AddMinutes(1))).IsResolved.Should().BeTrue();
    }

    public static TheoryData<string> MalformedSkus => new()
    {
        "",
        "   ",
        new string('x', 101),
        "sku\u0000with-null",
        "line\nbreak"
    };

    [Theory]
    [MemberData(nameof(MalformedSkus))]
    public async Task MalformedSkusAreRejectedOnBothPaths(string sku)
    {
        var product = await _db.SeedProductAsync();

        var map = () => _service.MapAsync(sku, product, T0, _actor);
        var resolve = () => _service.ResolveAsync(sku, T0);

        await map.Should().ThrowAsync<InvalidProductMappingRequestException>();
        await resolve.Should().ThrowAsync<InvalidProductMappingRequestException>();
    }

    [Fact]
    public async Task AnActorIsRequiredAndSqlInTheSkuIsJustData()
    {
        var product = await _db.SeedProductAsync();
        const string hostile = "x'; DROP TABLE catalog.products; --";

        var anonymous = () => _service.MapAsync(Sku(), product, T0, Guid.Empty);
        await anonymous.Should().ThrowAsync<InvalidProductMappingRequestException>();

        await _service.MapAsync(hostile, product, T0, _actor);
        (await _service.ResolveAsync(hostile, T0.AddMinutes(1))).ProductId.Should().Be(product);
    }
}
