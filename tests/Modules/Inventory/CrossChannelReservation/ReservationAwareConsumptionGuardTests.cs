using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.CrossChannelReservation.Tests;

/// <summary>
/// The consumption side, driven exactly the way order acceptance drives it
/// (OrderStockConsumptionService): on-hand row lock, this guard, then the guarded on-hand delta.
/// </summary>
public sealed class ReservationAwareConsumptionGuardTests : IClassFixture<CrossChannelReservationTestDatabase>
{
    private readonly CrossChannelReservationTestDatabase _db;
    private readonly PostgresCrossChannelPortionArbiter _arbiter;
    private readonly PostgresReservationAwareConsumptionGuard _guard = new();

    public ReservationAwareConsumptionGuardTests(CrossChannelReservationTestDatabase db)
    {
        _db = db;
        _arbiter = db.CreateArbiter();
    }

    private Task<bool> SellDirectlyAsync(StockFixture stock, Guid orderItemId, decimal quantity = 1m) =>
        _db.InTransactionAsync(async (connection, transaction) =>
        {
            var balances = _db.Balances;
            await balances.AcquireOnHandLockAsync(stock.StockItemId, stock.LocationId, connection, transaction);
            if (!await _guard.ConvertOwnHoldsAndCheckAvailableAsync(
                    orderItemId, stock.StockItemId, stock.LocationId, quantity, Guid.NewGuid(), connection, transaction))
                return false;
            return await balances.TryApplyGuardedOnHandDeltaAsync(
                stock.StockItemId, stock.LocationId, -quantity, connection, transaction) is not null;
        });

    private Task<CrossChannelReservationResult> HoldAsync(StockFixture stock, Guid orderItemId, ReservationChannel channel = ReservationChannel.Online) =>
        _db.InTransactionAsync((connection, transaction) => _arbiter.ReserveAsync(
            new CrossChannelReservationRequest(
                channel, "ref-" + Guid.NewGuid().ToString("N"), Guid.NewGuid(), Guid.NewGuid(),
                new[] { new CrossChannelReservationLine(orderItemId, stock.ProductId, 1m) }),
            connection, transaction));

    [Fact]
    public async Task ADirectSaleCannotTakeAPortionAnotherOrderHolds()
    {
        var stock = await _db.SeedStockAsync(onHand: 1m);
        (await HoldAsync(stock, Guid.NewGuid())).Outcome.Should().Be(CrossChannelReservationOutcome.Reserved);

        var sold = await SellDirectlyAsync(stock, Guid.NewGuid());

        sold.Should().BeFalse();
        (await _db.BalanceAsync(stock)).Should().Be((1m, 1m, 0m));
    }

    [Fact]
    public async Task AnOrdersOwnHoldBecomesItsConsumption()
    {
        var stock = await _db.SeedStockAsync(onHand: 1m);
        var orderItemId = Guid.NewGuid();
        await HoldAsync(stock, orderItemId);

        var sold = await SellDirectlyAsync(stock, orderItemId);

        sold.Should().BeTrue();
        (await _db.BalanceAsync(stock)).Should().Be((0m, 0m, 0m));
        (await _db.ReservationsForAsync(stock.StockItemId)).Should().ContainSingle().Which.Status.Should().Be("Consumed");
        (await _db.CreateProjector().DetectDriftAsync()).Drifts.Should().NotContain(d => d.StockItemId == stock.StockItemId);
    }

    [Fact]
    public async Task AConsumedHoldIsNeverConvertedTwice()
    {
        var stock = await _db.SeedStockAsync(onHand: 3m);
        var orderItemId = Guid.NewGuid();
        await HoldAsync(stock, orderItemId);
        await HoldAsync(stock, Guid.NewGuid());
        (await SellDirectlyAsync(stock, orderItemId)).Should().BeTrue();

        // A second round for the same line finds no active hold of its own; the
        // other order's hold still counts, so only one more portion is free.
        (await SellDirectlyAsync(stock, orderItemId)).Should().BeTrue();
        (await SellDirectlyAsync(stock, orderItemId)).Should().BeFalse();
        (await _db.BalanceAsync(stock)).Should().Be((1m, 1m, 0m));
    }

    [Fact]
    public async Task WithoutHoldsADirectSaleSeesTheWholeOnHand()
    {
        var stock = await _db.SeedStockAsync(onHand: 2m);

        (await SellDirectlyAsync(stock, Guid.NewGuid(), 2m)).Should().BeTrue();
        (await SellDirectlyAsync(stock, Guid.NewGuid())).Should().BeFalse();
        (await _db.BalanceAsync(stock)).Should().Be((0m, 0m, 0m));
    }

    [Fact]
    public async Task AHoldAndADirectSaleRacingForTheLastPortionHaveExactlyOneWinner()
    {
        for (var round = 0; round < 10; round++)
        {
            var stock = await _db.SeedStockAsync(onHand: 1m);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var hold = Task.Run(async () => { await start.Task; return await HoldAsync(stock, Guid.NewGuid()); });
            var sale = Task.Run(async () => { await start.Task; return await SellDirectlyAsync(stock, Guid.NewGuid()); });
            start.SetResult();

            var held = (await hold).Outcome == CrossChannelReservationOutcome.Reserved;
            var sold = await sale;

            (held ^ sold).Should().BeTrue($"round {round}: exactly one channel may win the last portion");
            (await _db.BalanceAsync(stock)).Should().Be(held ? (1m, 1m, 0m) : (0m, 0m, 0m));
        }
    }
}
