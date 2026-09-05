using ALKAROS.Inventory.PortionReservations.Lifecycle;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.PortionReservations.Concurrency.Tests;

public sealed class FakePortionReservationArbitratorRepository : IPortionReservationArbitratorRepository
{
    private readonly object _lock = new();
    private readonly Dictionary<(Guid Item, Guid Location), decimal> _availableBalances = new();
    private readonly Dictionary<string, PortionReservation> _idempotencyIndex = new();

    public void SetAvailable(Guid itemId, Guid locationId, decimal available)
    {
        lock (_lock)
        {
            _availableBalances[(itemId, locationId)] = available;
        }
    }

    public decimal GetAvailable(Guid itemId, Guid locationId)
    {
        lock (_lock)
        {
            return _availableBalances.GetValueOrDefault((itemId, locationId), 0m);
        }
    }

    public Task<ReservationArbitrationResult> TryReserveAtomicAsync(ArbitrateReservationCommand command, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(command.IdempotencyKey) &&
                _idempotencyIndex.TryGetValue(command.IdempotencyKey.Trim(), out var existingRsv))
            {
                var curAvail = _availableBalances.GetValueOrDefault((command.StockItemId, command.StockLocationId), 0m);
                return Task.FromResult(ReservationArbitrationResult.Replay(existingRsv, curAvail));
            }

            var key = (command.StockItemId, command.StockLocationId);
            var cur = _availableBalances.GetValueOrDefault(key, 0m);

            if (cur < command.Quantity)
            {
                return Task.FromResult(ReservationArbitrationResult.OutOfStock(cur));
            }

            var remaining = cur - command.Quantity;
            _availableBalances[key] = remaining;

            var rsv = PortionReservation.Create(
                command.OrderId,
                command.OrderItemId,
                command.StockItemId,
                command.StockLocationId,
                command.Quantity,
                command.UnitCode,
                command.ActorId,
                command.IdempotencyKey,
                command.MetadataJson);

            if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
            {
                _idempotencyIndex[command.IdempotencyKey.Trim()] = rsv;
            }

            return Task.FromResult(ReservationArbitrationResult.Success(rsv, remaining));
        }
    }
}

public sealed class PortionReservationConcurrencyDomainTests
{
    private readonly FakePortionReservationArbitratorRepository _repo;
    private readonly PortionReservationArbitrator _arbitrator;
    private readonly Guid _stockItemId = Guid.NewGuid();
    private readonly Guid _stockLocationId = Guid.NewGuid();
    private readonly Guid _staffId = Guid.NewGuid();

    public PortionReservationConcurrencyDomainTests()
    {
        _repo = new FakePortionReservationArbitratorRepository();
        _arbitrator = new PortionReservationArbitrator(_repo);
    }

    [Fact]
    public async Task ArbitrateReservationWhenSufficientStockReturnsReserved()
    {
        _repo.SetAvailable(_stockItemId, _stockLocationId, 5m);

        var cmd = new ArbitrateReservationCommand(
            OrderId: Guid.NewGuid(),
            OrderItemId: Guid.NewGuid(),
            StockItemId: _stockItemId,
            StockLocationId: _stockLocationId,
            Quantity: 2m,
            UnitCode: "portion",
            ActorId: _staffId);

        var result = await _arbitrator.ArbitrateReservationAsync(cmd);

        result.Status.Should().Be(ArbitrationStatus.Reserved);
        result.IsSuccess.Should().BeTrue();
        result.Reservation.Should().NotBeNull();
        result.Reservation!.Quantity.Should().Be(2m);
        result.RemainingAvailableQuantity.Should().Be(3m);
        _repo.GetAvailable(_stockItemId, _stockLocationId).Should().Be(3m);
    }

    [Fact]
    public async Task ArbitrateReservationWhenInsufficientStockReturnsOutOfStock()
    {
        _repo.SetAvailable(_stockItemId, _stockLocationId, 1m);

        var cmd = new ArbitrateReservationCommand(
            OrderId: Guid.NewGuid(),
            OrderItemId: Guid.NewGuid(),
            StockItemId: _stockItemId,
            StockLocationId: _stockLocationId,
            Quantity: 2m,
            UnitCode: "portion",
            ActorId: _staffId);

        var result = await _arbitrator.ArbitrateReservationAsync(cmd);

        result.Status.Should().Be(ArbitrationStatus.OutOfStock);
        result.IsSuccess.Should().BeFalse();
        result.Reservation.Should().BeNull();
        result.RemainingAvailableQuantity.Should().Be(1m);
        result.FailureReason.Should().NotBeNullOrWhiteSpace();
        _repo.GetAvailable(_stockItemId, _stockLocationId).Should().Be(1m);
    }

    [Fact]
    public async Task ArbitrateReservationWhenDuplicateIdempotencyKeyReturnsIdempotentReplay()
    {
        _repo.SetAvailable(_stockItemId, _stockLocationId, 5m);

        var cmd = new ArbitrateReservationCommand(
            OrderId: Guid.NewGuid(),
            OrderItemId: Guid.NewGuid(),
            StockItemId: _stockItemId,
            StockLocationId: _stockLocationId,
            Quantity: 2m,
            UnitCode: "portion",
            ActorId: _staffId,
            IdempotencyKey: "rsv-idemp-1");

        var first = await _arbitrator.ArbitrateReservationAsync(cmd);
        var second = await _arbitrator.ArbitrateReservationAsync(cmd);

        first.Status.Should().Be(ArbitrationStatus.Reserved);
        second.Status.Should().Be(ArbitrationStatus.IdempotentReplay);
        second.IsSuccess.Should().BeTrue();
        second.Reservation!.Id.Should().Be(first.Reservation!.Id);
        _repo.GetAvailable(_stockItemId, _stockLocationId).Should().Be(3m);
    }

    [Fact]
    public async Task ArbitrateReservationWithInvalidQuantityThrowsInvalidArbitrationCommandException()
    {
        var cmd = new ArbitrateReservationCommand(
            OrderId: Guid.NewGuid(),
            OrderItemId: Guid.NewGuid(),
            StockItemId: _stockItemId,
            StockLocationId: _stockLocationId,
            Quantity: 0m,
            UnitCode: "portion",
            ActorId: _staffId);

        var act = () => _arbitrator.ArbitrateReservationAsync(cmd);
        await act.Should().ThrowAsync<InvalidArbitrationCommandException>();
    }

    [Fact]
    public async Task ArbitrateReservationWithEmptyActorThrowsInvalidArbitrationCommandException()
    {
        var cmd = new ArbitrateReservationCommand(
            OrderId: Guid.NewGuid(),
            OrderItemId: Guid.NewGuid(),
            StockItemId: _stockItemId,
            StockLocationId: _stockLocationId,
            Quantity: 1m,
            UnitCode: "portion",
            ActorId: Guid.Empty);

        var act = () => _arbitrator.ArbitrateReservationAsync(cmd);
        await act.Should().ThrowAsync<InvalidArbitrationCommandException>();
    }

    [Fact]
    public async Task ParallelArbitrationForSingleRemainingPortionAllowsExactlyOneWinner()
    {
        _repo.SetAvailable(_stockItemId, _stockLocationId, 1m);

        var cmd1 = new ArbitrateReservationCommand(
            OrderId: Guid.NewGuid(),
            OrderItemId: Guid.NewGuid(),
            StockItemId: _stockItemId,
            StockLocationId: _stockLocationId,
            Quantity: 1m,
            UnitCode: "portion",
            ActorId: _staffId,
            IdempotencyKey: "client-a");

        var cmd2 = new ArbitrateReservationCommand(
            OrderId: Guid.NewGuid(),
            OrderItemId: Guid.NewGuid(),
            StockItemId: _stockItemId,
            StockLocationId: _stockLocationId,
            Quantity: 1m,
            UnitCode: "portion",
            ActorId: _staffId,
            IdempotencyKey: "client-b");

        var task1 = Task.Run(() => _arbitrator.ArbitrateReservationAsync(cmd1));
        var task2 = Task.Run(() => _arbitrator.ArbitrateReservationAsync(cmd2));

        var results = await Task.WhenAll(task1, task2);

        var reservedCount = results.Count(r => r.Status == ArbitrationStatus.Reserved);
        var outOfStockCount = results.Count(r => r.Status == ArbitrationStatus.OutOfStock);

        reservedCount.Should().Be(1);
        outOfStockCount.Should().Be(1);

        _repo.GetAvailable(_stockItemId, _stockLocationId).Should().Be(0m);
    }
}
