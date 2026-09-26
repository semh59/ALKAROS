using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.CrossChannelReservation.Tests;

/// <summary>
/// V12-RMD-003: a consumed order is never "held" again, and the last portion goes to exactly one buyer when the
/// channels compete the way they do in production — cashier and waiter sales through the consumption guard,
/// QR and online orders through the arbiter.
/// </summary>
public sealed class ArbiterHardeningTests : IClassFixture<CrossChannelReservationTestDatabase>
{
    private readonly CrossChannelReservationTestDatabase _db;
    private readonly PostgresCrossChannelPortionArbiter _arbiter;
    private readonly PostgresReservationAwareConsumptionGuard _guard = new();

    public ArbiterHardeningTests(CrossChannelReservationTestDatabase db)
    {
        _db = db;
        _arbiter = db.CreateArbiter();
    }

    private static CrossChannelReservationRequest Request(ReservationChannel channel, Guid productId) =>
        new(channel, $"{channel}-{Guid.NewGuid():N}", Guid.NewGuid(), Guid.NewGuid(),
            [new CrossChannelReservationLine(Guid.NewGuid(), productId, 1m)]);

    /// <summary>A direct sale the way acceptance consumes: lock the row, check availability, take on-hand.</summary>
    private Task<bool> SellDirectlyAsync(StockFixture stock) =>
        _db.InTransactionAsync(async (connection, transaction) =>
        {
            await _db.Balances.AcquireOnHandLockAsync(stock.StockItemId, stock.LocationId, connection, transaction);
            if (!await _guard.ConvertOwnHoldsAndCheckAvailableAsync(
                    Guid.NewGuid(), stock.StockItemId, stock.LocationId, 1m, Guid.NewGuid(), connection, transaction))
                return false;
            return await _db.Balances.TryApplyGuardedOnHandDeltaAsync(stock.StockItemId, stock.LocationId, -1m, connection, transaction) is not null;
        });

    [Fact]
    public async Task AnOrderWhoseHoldsWereConsumedIsNeverHeldAgain()
    {
        var stock = await _db.SeedStockAsync(onHand: 3m);
        var request = Request(ReservationChannel.Qr, stock.ProductId);
        await _db.InTransactionAsync(async (connection, transaction) =>
        {
            await _arbiter.ReserveAsync(request, connection, transaction);
            return await _guard.ConvertOwnHoldsAndCheckAvailableAsync(
                request.Lines[0].OrderItemId, stock.StockItemId, stock.LocationId, 1m, request.ActorId, connection, transaction);
        });

        var repeat = await _db.InTransactionAsync((connection, transaction) => _arbiter.ReserveAsync(request, connection, transaction));

        repeat.Outcome.Should().Be(CrossChannelReservationOutcome.AlreadyConsumed);
        repeat.IsHeld.Should().BeFalse();
        repeat.Holds.Should().BeEmpty();
        (await _db.BalanceAsync(stock)).Should().Be((3m, 0m, 3m));
    }

    [Fact]
    public async Task TheLastPortionGoesToExactlyOneBuyerWhenSalesAndHoldsRaceAsInProduction()
    {
        for (var round = 0; round < 10; round++)
        {
            var stock = await _db.SeedStockAsync(onHand: 1m);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var buyers = new List<Task<bool>>
            {
                Run(() => SellDirectlyAsync(stock)),                       // cashier
                Run(() => SellDirectlyAsync(stock)),                       // waiter
                Run(() => HoldAsync(ReservationChannel.Qr, stock)),
                Run(() => HoldAsync(ReservationChannel.Online, stock)),
            };
            start.SetResult();
            var won = await Task.WhenAll(buyers);

            won.Count(w => w).Should().Be(1, $"round {round}");
            var (onHand, reserved, available) = await _db.BalanceAsync(stock);
            available.Should().Be(0m);
            (onHand + reserved).Should().BeOneOf(0m + 0m, 1m + 1m);

            async Task<bool> Run(Func<Task<bool>> buy)
            {
                await start.Task;
                return await buy();
            }
        }
    }

    private async Task<bool> HoldAsync(ReservationChannel channel, StockFixture stock)
    {
        var result = await _db.InTransactionAsync((connection, transaction) =>
            _arbiter.ReserveAsync(Request(channel, stock.ProductId), connection, transaction));
        return result.IsHeld;
    }
}
