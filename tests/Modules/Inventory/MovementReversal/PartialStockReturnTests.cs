using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Measurements;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Inventory.MovementReversal.Tests;

public sealed class PartialStockReturnTests : IClassFixture<MovementReversalTestDb>
{
    private readonly MovementReversalTestDb _db;
    private readonly PostgresStockBalanceRepository _balances;
    private readonly PostgresStockMovementRepository _movements;
    private readonly StockMovementReversalService _service;
    private readonly StockMovementService _recorder;
    private readonly StockMasterService _master;
    private readonly StockBalanceProjector _projector;

    public PartialStockReturnTests(MovementReversalTestDb db)
    {
        _db = db;
        var locations = new PostgresStockLocationRepository(db.DataSource);
        var items = new PostgresStockItemRepository(db.DataSource);
        _movements = new PostgresStockMovementRepository(db.DataSource);
        _balances = new PostgresStockBalanceRepository(db.DataSource);
        _projector = new StockBalanceProjector(_balances, _movements, locations);
        _service = new StockMovementReversalService(_movements, items, locations, _projector, _balances);
        var converter = new UnitConverter();
        _master = new StockMasterService(locations, items, new PostgresProductStockMappingRepository(db.DataSource), converter);
        _recorder = new StockMovementService(_movements, items, locations, converter);
    }

    [Fact]
    public async Task ReturnsTheMatchingShareOfEachIngredientAndRaisesTheBalance()
    {
        var location = await NewLocationAsync();
        var beef = await NewItemAsync("Beef");
        var bun = await NewItemAsync("Bun");
        var orderItemId = Guid.NewGuid();
        await StockAsync(beef.Id, location.Id, 100m);
        await StockAsync(bun.Id, location.Id, 50m);
        await ConsumeAsync(beef.Id, location.Id, 30m, orderItemId);
        await ConsumeAsync(bun.Id, location.Id, 3m, orderItemId);

        var written = await ReturnAsync(orderItemId, returned: 1m, previously: 0m, total: 3m);

        written.Should().HaveCount(2);
        written.Single(m => m.StockItemId == beef.Id).Quantity.Should().Be(10m);
        written.Single(m => m.StockItemId == bun.Id).Quantity.Should().Be(1m);
        written.Should().OnlyContain(m => m.MovementType == StockMovementType.Return
            && m.Direction == MovementDirection.In && m.SourceType == StockMovementSourceType.Order
            && m.SourceReferenceId == orderItemId);
        (await _balances.GetByItemAndLocationAsync(beef.Id, location.Id))!.OnHandQuantity.Should().Be(80m);
        (await _balances.GetByItemAndLocationAsync(bun.Id, location.Id))!.OnHandQuantity.Should().Be(48m);
    }

    [Fact]
    public async Task TheCumulativeShareKeepsTheRoundingFromDriftingSoTheTotalIsExactlyWhatWasConsumed()
    {
        var location = await NewLocationAsync();
        var item = await NewItemAsync("Sauce");
        var orderItemId = Guid.NewGuid();
        await StockAsync(item.Id, location.Id, 100m);
        await ConsumeAsync(item.Id, location.Id, 10m, orderItemId);

        var first = await ReturnAsync(orderItemId, 1m, 0m, 3m);
        var second = await ReturnAsync(orderItemId, 1m, 1m, 3m);
        var third = await ReturnAsync(orderItemId, 1m, 2m, 3m);

        first.Single().Quantity.Should().Be(3.3333m);
        second.Single().Quantity.Should().Be(3.3334m);
        third.Single().Quantity.Should().Be(3.3333m);
        (await _balances.GetByItemAndLocationAsync(item.Id, location.Id))!.OnHandQuantity.Should().Be(100m);
    }

    [Fact]
    public async Task NeverReturnsMoreThanWasConsumedEvenWhenAskedAgain()
    {
        var location = await NewLocationAsync();
        var item = await NewItemAsync("Cheese");
        var orderItemId = Guid.NewGuid();
        await StockAsync(item.Id, location.Id, 100m);
        await ConsumeAsync(item.Id, location.Id, 4m, orderItemId);
        await ReturnAsync(orderItemId, 2m, 0m, 2m);

        var again = await ReturnAsync(orderItemId, 1m, 0m, 2m);

        again.Should().BeEmpty();
        (await _balances.GetByItemAndLocationAsync(item.Id, location.Id))!.OnHandQuantity.Should().Be(100m);
    }

    [Fact]
    public async Task LeavesAConsumptionThatWasAlreadyReversedAsAWholeAlone()
    {
        var location = await NewLocationAsync();
        var item = await NewItemAsync("Egg");
        var orderItemId = Guid.NewGuid();
        await StockAsync(item.Id, location.Id, 100m);
        var consumption = await ConsumeAsync(item.Id, location.Id, 6m, orderItemId);
        await _service.ReverseMovementAsync(new StockMovementReversalRequest(consumption.Id, "Voided"));

        var written = await ReturnAsync(orderItemId, 1m, 0m, 2m);

        written.Should().BeEmpty();
        (await _balances.GetByItemAndLocationAsync(item.Id, location.Id))!.OnHandQuantity.Should().Be(100m);
    }

    [Fact]
    public async Task RejectsAnImpossibleQuantityOrAnEmptyReason()
    {
        var orderItemId = Guid.NewGuid();
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var tooMany = () => _service.ReturnConsumedShareAsync(
            new StockShareReturnRequest(orderItemId, 2m, 2m, 3m, "x"), connection, transaction);
        var none = () => _service.ReturnConsumedShareAsync(
            new StockShareReturnRequest(orderItemId, 0m, 0m, 3m, "x"), connection, transaction);
        var noReason = () => _service.ReturnConsumedShareAsync(
            new StockShareReturnRequest(orderItemId, 1m, 0m, 3m, " "), connection, transaction);
        var negativePrevious = () => _service.ReturnConsumedShareAsync(
            new StockShareReturnRequest(orderItemId, 1m, -1m, 3m, "x"), connection, transaction);

        await tooMany.Should().ThrowAsync<InvalidStockMovementException>();
        await none.Should().ThrowAsync<InvalidStockMovementException>();
        await noReason.Should().ThrowAsync<InvalidReversalReasonException>();
        await negativePrevious.Should().ThrowAsync<InvalidStockMovementException>();
    }

    [Fact]
    public async Task RollsBackWithTheCallersTransaction()
    {
        var location = await NewLocationAsync();
        var item = await NewItemAsync("Milk");
        var orderItemId = Guid.NewGuid();
        await StockAsync(item.Id, location.Id, 100m);
        await ConsumeAsync(item.Id, location.Id, 10m, orderItemId);
        await using (var connection = await _db.DataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var written = await _service.ReturnConsumedShareAsync(
                new StockShareReturnRequest(orderItemId, 1m, 0m, 2m, "Müşteri vazgeçti"), connection, transaction);
            written.Should().ContainSingle();
            await transaction.RollbackAsync();
        }

        (await _balances.GetByItemAndLocationAsync(item.Id, location.Id))!.OnHandQuantity.Should().Be(90m);
        (await _movements.GetBySourceAsync(StockMovementSourceType.Order, orderItemId))
            .Should().NotContain(m => m.MovementType == StockMovementType.Return);
    }

    private async Task<IReadOnlyList<StockMovement>> ReturnAsync(Guid orderItemId, decimal returned, decimal previously, decimal total)
    {
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var written = await _service.ReturnConsumedShareAsync(
            new StockShareReturnRequest(orderItemId, returned, previously, total, "Müşteri vazgeçti"), connection, transaction);
        await transaction.CommitAsync();
        return written;
    }

    private async Task<StockMovement> ConsumeAsync(Guid itemId, Guid locationId, decimal quantity, Guid orderItemId)
    {
        var movement = await _recorder.RecordMovementAsync(
            itemId, locationId, StockMovementType.Consumption, quantity, "kg", StockMovementSourceType.Order,
            sourceReferenceId: orderItemId, reason: "Order accepted");
        await _projector.ApplyMovementAsync(movement);
        return movement;
    }

    private async Task StockAsync(Guid itemId, Guid locationId, decimal quantity)
    {
        var receipt = await _recorder.RecordMovementAsync(
            itemId, locationId, StockMovementType.PurchaseReceipt, quantity, "kg", StockMovementSourceType.PurchaseOrder);
        await _projector.ApplyMovementAsync(receipt);
    }

    private Task<StockLocation> NewLocationAsync()
        => _master.CreateLocationAsync("LOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), "Kitchen", StockLocationType.Kitchen);

    private Task<StockItem> NewItemAsync(string name)
        => _master.CreateStockItemAsync("SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), name, StockItemType.RawMaterial, "kg");
}
