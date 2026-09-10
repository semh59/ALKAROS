using ALKAROS.Inventory.StockMaster;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.PortionReservations.Lifecycle.Tests;

public sealed class PortionReservationLifecycleDomainTests
{
    private static (
        PortionReservationLifecycleService service,
        FakePortionReservationRepository reservationRepo,
        FakeStockItemRepository itemRepo,
        FakeStockLocationRepository locRepo) CreateTestHarness()
    {
        var reservationRepo = new FakePortionReservationRepository();
        var itemRepo = new FakeStockItemRepository();
        var locRepo = new FakeStockLocationRepository();
        var service = new PortionReservationLifecycleService(reservationRepo, itemRepo, locRepo);

        return (service, reservationRepo, itemRepo, locRepo);
    }

    [Fact]
    public async Task CreateReservationWithValidDataInitializesInReservedStatus()
    {
        var (service, _, itemRepo, locRepo) = CreateTestHarness();

        var item = StockItem.Create("SKU-RSV-1", "Steak", StockItemType.RawMaterial, "portion");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-RSV-1", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);

        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        var cmd = new CreateReservationCommand(
            OrderId: orderId,
            OrderItemId: orderItemId,
            StockItemId: item.Id,
            StockLocationId: loc.Id,
            Quantity: 2m,
            UnitCode: "portion",
            CreatedBy: staffId,
            IdempotencyKey: "rsv-idemp-1");

        var result = await service.CreateReservationAsync(cmd);

        result.IsIdempotentReplay.Should().BeFalse();
        result.Reservation.Status.Should().Be(PortionReservationStatus.Reserved);
        result.Reservation.Quantity.Should().Be(2m);
        result.Reservation.OrderId.Should().Be(orderId);
        result.Reservation.OrderItemId.Should().Be(orderItemId);
        result.Reservation.Version.Should().Be(1);
    }

    [Fact]
    public async Task ReleaseReservationTransitionsStatusToReleased()
    {
        var (service, _, itemRepo, locRepo) = CreateTestHarness();

        var item = StockItem.Create("SKU-RSV-2", "Soup", StockItemType.RawMaterial, "portion");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-RSV-2", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);

        var actorId = Guid.NewGuid();
        var createResult = await service.CreateReservationAsync(new CreateReservationCommand(
            Guid.NewGuid(), Guid.NewGuid(), item.Id, loc.Id, 1m, "portion", actorId));

        var releaseResult = await service.ReleaseReservationAsync(new TransitionReservationCommand(
            createResult.Reservation.Id, PortionReservationStatus.Released, actorId, "Customer cancelled before cooking"));

        releaseResult.IsIdempotentReplay.Should().BeFalse();
        releaseResult.Reservation.Status.Should().Be(PortionReservationStatus.Released);
        releaseResult.Reservation.Version.Should().Be(2);
        releaseResult.Reservation.TransitionReason.Should().Be("Customer cancelled before cooking");
    }

    [Fact]
    public async Task ConsumeReservationTransitionsStatusToConsumed()
    {
        var (service, _, itemRepo, locRepo) = CreateTestHarness();

        var item = StockItem.Create("SKU-RSV-3", "Pasta", StockItemType.RawMaterial, "portion");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-RSV-3", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);

        var actorId = Guid.NewGuid();
        var createResult = await service.CreateReservationAsync(new CreateReservationCommand(
            Guid.NewGuid(), Guid.NewGuid(), item.Id, loc.Id, 1m, "portion", actorId));

        var consumeResult = await service.ConsumeReservationAsync(new TransitionReservationCommand(
            createResult.Reservation.Id, PortionReservationStatus.Consumed, actorId, "Order served"));

        consumeResult.IsIdempotentReplay.Should().BeFalse();
        consumeResult.Reservation.Status.Should().Be(PortionReservationStatus.Consumed);
        consumeResult.Reservation.Version.Should().Be(2);
    }

    [Fact]
    public async Task WasteReservationTransitionsStatusToWaste()
    {
        var (service, _, itemRepo, locRepo) = CreateTestHarness();

        var item = StockItem.Create("SKU-RSV-4", "Burger", StockItemType.RawMaterial, "portion");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-RSV-4", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);

        var actorId = Guid.NewGuid();
        var createResult = await service.CreateReservationAsync(new CreateReservationCommand(
            Guid.NewGuid(), Guid.NewGuid(), item.Id, loc.Id, 1m, "portion", actorId));

        var wasteResult = await service.WasteReservationAsync(new TransitionReservationCommand(
            createResult.Reservation.Id, PortionReservationStatus.Waste, actorId, "Burnt during preparation"));

        wasteResult.IsIdempotentReplay.Should().BeFalse();
        wasteResult.Reservation.Status.Should().Be(PortionReservationStatus.Waste);
        wasteResult.Reservation.Version.Should().Be(2);
    }

    [Fact]
    public async Task TransitionFromTerminalStateThrowsInvalidPortionReservationTransitionException()
    {
        var (service, _, itemRepo, locRepo) = CreateTestHarness();

        var item = StockItem.Create("SKU-RSV-5", "Pizza", StockItemType.RawMaterial, "portion");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-RSV-5", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);

        var actorId = Guid.NewGuid();
        var createResult = await service.CreateReservationAsync(new CreateReservationCommand(
            Guid.NewGuid(), Guid.NewGuid(), item.Id, loc.Id, 1m, "portion", actorId));

        // Consume it
        await service.ConsumeReservationAsync(new TransitionReservationCommand(
            createResult.Reservation.Id, PortionReservationStatus.Consumed, actorId));

        // Attempting to release an already consumed reservation must throw
        var actRelease = () => service.ReleaseReservationAsync(new TransitionReservationCommand(
            createResult.Reservation.Id, PortionReservationStatus.Released, actorId));
        await actRelease.Should().ThrowAsync<InvalidPortionReservationTransitionException>()
            .WithMessage("*terminal status 'Consumed'*");

        // Attempting to waste an already consumed reservation must throw
        var actWaste = () => service.WasteReservationAsync(new TransitionReservationCommand(
            createResult.Reservation.Id, PortionReservationStatus.Waste, actorId));
        await actWaste.Should().ThrowAsync<InvalidPortionReservationTransitionException>()
            .WithMessage("*terminal status 'Consumed'*");
    }

    [Fact]
    public async Task RepeatingSameTransitionIsIdempotentReplay()
    {
        var (service, _, itemRepo, locRepo) = CreateTestHarness();

        var item = StockItem.Create("SKU-RSV-6", "Salad", StockItemType.RawMaterial, "portion");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-RSV-6", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);

        var actorId = Guid.NewGuid();
        var createResult = await service.CreateReservationAsync(new CreateReservationCommand(
            Guid.NewGuid(), Guid.NewGuid(), item.Id, loc.Id, 1m, "portion", actorId));

        var cmd = new TransitionReservationCommand(
            createResult.Reservation.Id, PortionReservationStatus.Released, actorId, "Customer change of mind");

        var first = await service.ReleaseReservationAsync(cmd);
        first.IsIdempotentReplay.Should().BeFalse();

        var second = await service.ReleaseReservationAsync(cmd);
        second.IsIdempotentReplay.Should().BeTrue();
        second.Reservation.Status.Should().Be(PortionReservationStatus.Released);
        second.Reservation.Version.Should().Be(first.Reservation.Version);
    }

    [Fact]
    public async Task CreateReservationWithDuplicateIdempotencyKeyReplaysOriginal()
    {
        var (service, _, itemRepo, locRepo) = CreateTestHarness();

        var item = StockItem.Create("SKU-RSV-7", "Fish", StockItemType.RawMaterial, "portion");
        await itemRepo.AddAsync(item);
        var loc = StockLocation.Create("LOC-RSV-7", "Kitchen", StockLocationType.Kitchen);
        await locRepo.AddAsync(loc);

        var cmd = new CreateReservationCommand(
            Guid.NewGuid(), Guid.NewGuid(), item.Id, loc.Id, 1m, "portion", Guid.NewGuid(), IdempotencyKey: "dup-key-1");

        var res1 = await service.CreateReservationAsync(cmd);
        res1.IsIdempotentReplay.Should().BeFalse();

        var res2 = await service.CreateReservationAsync(cmd);
        res2.IsIdempotentReplay.Should().BeTrue();
        res2.Reservation.Id.Should().Be(res1.Reservation.Id);
    }

    public sealed class FakePortionReservationRepository : IPortionReservationRepository
    {
        private readonly Dictionary<Guid, PortionReservation> _reservations = new();
        private readonly Dictionary<Guid, int> _versions = new();

        public Task InsertAsync(PortionReservation reservation, CancellationToken cancellationToken = default)
        {
            _reservations[reservation.Id] = Clone(reservation);
            _versions[reservation.Id] = reservation.Version;
            return Task.CompletedTask;
        }

        public Task<PortionReservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (_reservations.TryGetValue(id, out var res))
                return Task.FromResult<PortionReservation?>(Clone(res));
            return Task.FromResult<PortionReservation?>(null);
        }

        public Task<PortionReservation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        {
            var res = _reservations.Values.FirstOrDefault(r => string.Equals(r.IdempotencyKey, idempotencyKey, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(res != null ? Clone(res) : null);
        }

        public Task<IReadOnlyList<PortionReservation>> GetByOrderItemIdAsync(Guid orderItemId, CancellationToken cancellationToken = default)
        {
            var list = _reservations.Values.Where(r => r.OrderItemId == orderItemId).Select(Clone).ToList();
            return Task.FromResult<IReadOnlyList<PortionReservation>>(list);
        }

        public Task<IReadOnlyList<PortionReservation>> GetActiveByStockItemAndLocationAsync(Guid stockItemId, Guid stockLocationId, CancellationToken cancellationToken = default)
        {
            var list = _reservations.Values.Where(r => r.StockItemId == stockItemId && r.StockLocationId == stockLocationId && r.Status == PortionReservationStatus.Reserved).Select(Clone).ToList();
            return Task.FromResult<IReadOnlyList<PortionReservation>>(list);
        }

        public Task<bool> UpdateStatusOptimisticAsync(PortionReservation reservation, int expectedVersion, CancellationToken cancellationToken = default)
        {
            if (!_versions.TryGetValue(reservation.Id, out var currentVersion))
                return Task.FromResult(false);

            if (currentVersion != expectedVersion)
                return Task.FromResult(false);

            _reservations[reservation.Id] = Clone(reservation);
            _versions[reservation.Id] = reservation.Version;
            return Task.FromResult(true);
        }

        private static PortionReservation Clone(PortionReservation r)
        {
            return new PortionReservation(
                r.Id, r.OrderId, r.OrderItemId, r.StockItemId, r.StockLocationId,
                r.Quantity, r.UnitCode, r.Status, r.Version, r.ReservedAt,
                r.CreatedBy, r.IdempotencyKey, r.TransitionedAt, r.TransitionReason,
                r.TransitionedBy, r.MetadataJson);
        }
    }

    public sealed class FakeStockItemRepository : IStockItemRepository
    {
        private readonly Dictionary<Guid, StockItem> _items = new();

        public Task AddAsync(StockItem item, CancellationToken ct = default)
        {
            _items[item.Id] = item;
            return Task.CompletedTask;
        }

        public Task<StockItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_items.TryGetValue(id, out var it) ? it : null);

        public Task<IReadOnlyList<StockItem>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StockItem>>(_items.Values.Where(i => ids.Contains(i.Id)).ToList());

        public Task<StockItem?> GetByCodeAsync(string code, CancellationToken ct = default)
            => Task.FromResult(_items.Values.FirstOrDefault(i => string.Equals(i.Code, code, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<StockItem>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StockItem>>(_items.Values.Where(i => !activeOnly || i.IsActive).ToList());

        public Task<IReadOnlyList<StockItem>> GetByTypeAsync(StockItemType itemType, bool activeOnly = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StockItem>>(_items.Values.Where(i => i.ItemType == itemType && (!activeOnly || i.IsActive)).ToList());

        public Task UpdateAsync(StockItem item, CancellationToken ct = default)
        {
            _items[item.Id] = item;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            _items.Remove(id);
            return Task.CompletedTask;
        }
    }

    public sealed class FakeStockLocationRepository : IStockLocationRepository
    {
        private readonly Dictionary<Guid, StockLocation> _locations = new();

        public Task AddAsync(StockLocation location, CancellationToken ct = default)
        {
            _locations[location.Id] = location;
            return Task.CompletedTask;
        }

        public Task<StockLocation?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_locations.TryGetValue(id, out var loc) ? loc : null);

        public Task<StockLocation?> GetByCodeAsync(string code, CancellationToken ct = default)
            => Task.FromResult(_locations.Values.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<StockLocation>> GetAllAsync(bool activeOnly = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StockLocation>>(_locations.Values.Where(l => !activeOnly || l.IsActive).ToList());

        public Task UpdateAsync(StockLocation location, CancellationToken ct = default)
        {
            _locations[location.Id] = location;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            _locations.Remove(id);
            return Task.CompletedTask;
        }
    }
}
