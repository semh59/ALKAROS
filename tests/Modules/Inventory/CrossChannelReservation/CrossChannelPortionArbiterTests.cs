using ALKAROS.Inventory.PortionReservations.CancellationEffects;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.CrossChannelReservation.Tests;

public sealed class CrossChannelPortionArbiterTests : IClassFixture<CrossChannelReservationTestDatabase>
{
    private readonly CrossChannelReservationTestDatabase _db;
    private readonly PostgresCrossChannelPortionArbiter _arbiter;

    public CrossChannelPortionArbiterTests(CrossChannelReservationTestDatabase db)
    {
        _db = db;
        _arbiter = db.CreateArbiter();
    }

    private static CrossChannelReservationRequest Request(
        ReservationChannel channel,
        params CrossChannelReservationLine[] lines) =>
        new(channel, $"{channel}-{Guid.NewGuid():N}", Guid.NewGuid(), Guid.NewGuid(), lines);

    private static CrossChannelReservationLine Line(Guid productId, decimal quantity = 1m) =>
        new(Guid.NewGuid(), productId, quantity);

    private Task<CrossChannelReservationResult> ReserveCommittedAsync(CrossChannelReservationRequest request) =>
        _db.InTransactionAsync((connection, transaction) => _arbiter.ReserveAsync(request, connection, transaction));

    [Fact]
    public async Task FourChannelsRacingForTheLastPortionYieldOneHoldAndThreeOutOfStock()
    {
        var stock = await _db.SeedStockAsync(onHand: 1m);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enum.GetValues<ReservationChannel>()
            .Select(channel => Request(channel, Line(stock.ProductId)))
            .ToList();

        var racers = requests.Select(async request =>
        {
            await start.Task;
            return await ReserveCommittedAsync(request);
        }).ToList();
        start.SetResult();
        var results = await Task.WhenAll(racers);

        results.Count(r => r.Outcome == CrossChannelReservationOutcome.Reserved).Should().Be(1);
        var losers = results.Where(r => r.Outcome != CrossChannelReservationOutcome.Reserved).ToList();
        losers.Should().HaveCount(3);
        losers.Should().OnlyContain(r => r.Outcome == CrossChannelReservationOutcome.OutOfStock && r.Holds.Count == 0);
        losers.SelectMany(r => r.Shortages).Should().OnlyContain(s =>
            s.StockItemId == stock.StockItemId && s.RequiredQuantity == 1m && s.AvailableQuantity == 0m);

        (await _db.BalanceAsync(stock)).Should().Be((1m, 1m, 0m));
        (await _db.ReservationsForAsync(stock.StockItemId)).Should().ContainSingle()
            .Which.Status.Should().Be("Reserved");
    }

    [Fact]
    public async Task AnOrderIsHeldAllOrNothingAcrossItsLines()
    {
        var plenty = await _db.SeedStockAsync(onHand: 5m);
        var scarce = await _db.SeedStockAsync(onHand: 1m);

        var scarceLine = Line(scarce.ProductId, 2m);
        var result = await ReserveCommittedAsync(Request(
            ReservationChannel.Online, Line(plenty.ProductId), scarceLine));

        result.Outcome.Should().Be(CrossChannelReservationOutcome.OutOfStock);
        var shortage = result.Shortages.Should().ContainSingle().Subject;
        shortage.StockItemId.Should().Be(scarce.StockItemId);
        shortage.OrderItemIds.Should().Equal(scarceLine.OrderItemId);
        // Committed by the caller, and still nothing was written for the line that did fit.
        (await _db.BalanceAsync(plenty)).Should().Be((5m, 0m, 5m));
        (await _db.ReservationsForAsync(plenty.StockItemId)).Should().BeEmpty();
    }

    [Fact]
    public async Task LinesSharingAStockItemAreCheckedAgainstTheirCombinedNeed()
    {
        var stock = await _db.SeedStockAsync(onHand: 3m, multiplier: 2m);

        var result = await ReserveCommittedAsync(Request(
            ReservationChannel.Qr, Line(stock.ProductId), Line(stock.ProductId)));

        result.Outcome.Should().Be(CrossChannelReservationOutcome.OutOfStock);
        result.Shortages.Should().ContainSingle().Which.RequiredQuantity.Should().Be(4m);
        (await _db.BalanceAsync(stock)).Should().Be((3m, 0m, 3m));
    }

    [Fact]
    public async Task HoldsRollBackWithTheCallersTransaction()
    {
        var stock = await _db.SeedStockAsync(onHand: 1m);

        var result = await _db.InTransactionAsync(
            (connection, transaction) => _arbiter.ReserveAsync(Request(ReservationChannel.Waiter, Line(stock.ProductId)), connection, transaction),
            rollback: true);

        result.Outcome.Should().Be(CrossChannelReservationOutcome.Reserved);
        (await _db.BalanceAsync(stock)).Should().Be((1m, 0m, 1m));
        (await _db.ReservationsForAsync(stock.StockItemId)).Should().BeEmpty();
    }

    [Fact]
    public async Task AnUnmappedProductOrAStockItemWithoutLocationIsATypedRefusal()
    {
        var unlocated = await _db.SeedStockAsync(onHand: 0m, withDefaultLocation: false);
        var unmappedProduct = Guid.NewGuid();

        var result = await ReserveCommittedAsync(Request(
            ReservationChannel.Cashier, Line(unmappedProduct), Line(unlocated.ProductId)));

        result.Outcome.Should().Be(CrossChannelReservationOutcome.NotConfigured);
        result.UnconfiguredLines.Select(l => (l.ProductId, l.Gap)).Should().BeEquivalentTo(new[]
        {
            (unmappedProduct, StockConfigurationGap.ProductHasNoStockMapping),
            (unlocated.ProductId, StockConfigurationGap.StockItemHasNoDefaultLocation)
        });
        result.Holds.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplayingTheSameRequestHoldsOnlyOnce()
    {
        var stock = await _db.SeedStockAsync(onHand: 2m);
        var request = Request(ReservationChannel.Online, Line(stock.ProductId));

        var first = await ReserveCommittedAsync(request);
        var replay = await ReserveCommittedAsync(request);

        first.Outcome.Should().Be(CrossChannelReservationOutcome.Reserved);
        replay.Outcome.Should().Be(CrossChannelReservationOutcome.Replayed);
        replay.Holds.Select(h => h.ReservationId).Should().Equal(first.Holds.Select(h => h.ReservationId));
        (await _db.BalanceAsync(stock)).Should().Be((2m, 1m, 1m));
    }

    [Fact]
    public async Task ConcurrentReplaysOfOneOrderStillHoldOnlyOnce()
    {
        var stock = await _db.SeedStockAsync(onHand: 5m);
        var request = Request(ReservationChannel.Online, Line(stock.ProductId));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var racers = Enumerable.Range(0, 4).Select(async _ =>
        {
            await start.Task;
            return await ReserveCommittedAsync(request);
        }).ToList();
        start.SetResult();
        var results = await Task.WhenAll(racers);

        results.Count(r => r.Outcome == CrossChannelReservationOutcome.Reserved).Should().Be(1);
        results.Count(r => r.Outcome == CrossChannelReservationOutcome.Replayed).Should().Be(3);
        (await _db.BalanceAsync(stock)).Should().Be((5m, 1m, 4m));
    }

    [Fact]
    public async Task ReplayingWithDifferentLinesIsAConflict()
    {
        var stock = await _db.SeedStockAsync(onHand: 5m);
        var request = Request(ReservationChannel.Qr, Line(stock.ProductId));
        await ReserveCommittedAsync(request);

        var changed = request with { Lines = new[] { request.Lines[0] with { Quantity = 2m } } };
        var act = () => ReserveCommittedAsync(changed);

        await act.Should().ThrowAsync<CrossChannelReservationConflictException>();
        (await _db.BalanceAsync(stock)).Should().Be((5m, 1m, 4m));
    }

    [Fact]
    public async Task ProviderRejectionBeforeTheKitchenStartsReleasesTheHoldOnce()
    {
        var stock = await _db.SeedStockAsync(onHand: 1m);
        var request = Request(ReservationChannel.Online, Line(stock.ProductId));
        await ReserveCommittedAsync(request);

        var first = await _arbiter.CompensateAsync(request.OrderId, request.ActorId, "Sağlayıcı kabulü reddetti");
        var again = await _arbiter.CompensateAsync(request.OrderId, request.ActorId, "Sağlayıcı kabulü reddetti");

        first.Should().ContainSingle().Which.Should().Match<CancellationDecisionResult>(d =>
            d.Action == CancellationAction.Release && !d.IsIdempotentReplay);
        again.Should().ContainSingle().Which.IsIdempotentReplay.Should().BeTrue();
        (await _db.BalanceAsync(stock)).Should().Be((1m, 0m, 1m));

        // The released portion is immediately winnable again by another channel.
        var next = await ReserveCommittedAsync(Request(ReservationChannel.Cashier, Line(stock.ProductId)));
        next.Outcome.Should().Be(CrossChannelReservationOutcome.Reserved);
    }

    [Fact]
    public async Task RejectionAfterPreparationStartedIsWasteDecidedByTheCancellationLifecycle()
    {
        var stock = await _db.SeedStockAsync(onHand: 2m);
        var request = Request(ReservationChannel.Online, Line(stock.ProductId));
        await ReserveCommittedAsync(request);
        await _db.MarkKitchenStartedAsync(request.Lines[0].OrderItemId);

        var decisions = await _arbiter.CompensateAsync(request.OrderId, request.ActorId, "Hazırlık sonrası iptal");
        var replay = await _arbiter.CompensateAsync(request.OrderId, request.ActorId, "Hazırlık sonrası iptal");

        decisions.Should().ContainSingle().Which.Action.Should().Be(CancellationAction.Waste);
        replay.Should().ContainSingle().Which.IsIdempotentReplay.Should().BeTrue();
        // The wasted portion left on-hand exactly once; the hold is gone.
        (await _db.BalanceAsync(stock)).Should().Be((1m, 0m, 1m));
    }

    [Fact]
    public async Task HoldsAndTheirOutcomesMatchAFullProjectionRebuild()
    {
        var stock = await _db.SeedStockAsync(onHand: 4m);
        var kept = Request(ReservationChannel.Qr, Line(stock.ProductId));
        var released = Request(ReservationChannel.Online, Line(stock.ProductId));
        await ReserveCommittedAsync(kept);
        await ReserveCommittedAsync(released);
        await _arbiter.CompensateAsync(released.OrderId, released.ActorId, "Müşteri vazgeçti");

        var drift = await _db.CreateProjector().DetectDriftAsync();

        drift.Drifts.Should().NotContain(d => d.StockItemId == stock.StockItemId);
        (await _db.BalanceAsync(stock)).Should().Be((4m, 1m, 3m));
    }

    [Fact]
    public async Task TheChannelReferenceIsStoredAsDataNeverAsSql()
    {
        var stock = await _db.SeedStockAsync(onHand: 1m);
        const string hostile = "x'); DROP TABLE inventory.stock_balances; --\"}";
        var request = Request(ReservationChannel.Online, Line(stock.ProductId)) with { ChannelOrderReference = hostile };

        var result = await ReserveCommittedAsync(request);

        result.Outcome.Should().Be(CrossChannelReservationOutcome.Reserved);
        var stored = (await _db.ReservationsForAsync(stock.StockItemId)).Single();
        stored.Metadata.Should().Contain("\"channel\": \"Online\"").And.Contain("DROP TABLE");
        (await _db.BalanceAsync(stock)).Should().Be((1m, 1m, 0m));
    }

    [Fact]
    public async Task CompensationRequiresAnActorAndAReason()
    {
        var withoutActor = () => _arbiter.CompensateAsync(Guid.NewGuid(), Guid.Empty, "reason");
        var withoutReason = () => _arbiter.CompensateAsync(Guid.NewGuid(), Guid.NewGuid(), " ");

        await withoutActor.Should().ThrowAsync<InvalidCrossChannelReservationException>();
        await withoutReason.Should().ThrowAsync<InvalidCrossChannelReservationException>();
    }

    public static TheoryData<string, Func<CrossChannelReservationRequest, CrossChannelReservationRequest>> InvalidRequests => new()
    {
        { "unknown channel", r => r with { Channel = (ReservationChannel)99 } },
        { "blank reference", r => r with { ChannelOrderReference = " " } },
        { "reference too long", r => r with { ChannelOrderReference = new string('r', 201) } },
        { "empty order", r => r with { OrderId = Guid.Empty } },
        { "empty actor", r => r with { ActorId = Guid.Empty } },
        { "no lines", r => r with { Lines = Array.Empty<CrossChannelReservationLine>() } },
        { "zero quantity", r => r with { Lines = new[] { r.Lines[0] with { Quantity = 0m } } } },
        { "empty product", r => r with { Lines = new[] { r.Lines[0] with { ProductId = Guid.Empty } } } },
        { "duplicate order item", r => r with { Lines = new[] { r.Lines[0], r.Lines[0] with { ProductId = Guid.NewGuid() } } } }
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task MalformedRequestsAreRejectedBeforeTouchingStock(
        string _,
        Func<CrossChannelReservationRequest, CrossChannelReservationRequest> corrupt)
    {
        var stock = await _db.SeedStockAsync(onHand: 1m);
        var request = corrupt(Request(ReservationChannel.Cashier, Line(stock.ProductId)));

        var act = () => ReserveCommittedAsync(request);

        await act.Should().ThrowAsync<InvalidCrossChannelReservationException>();
        (await _db.BalanceAsync(stock)).Should().Be((1m, 0m, 1m));
    }
}
