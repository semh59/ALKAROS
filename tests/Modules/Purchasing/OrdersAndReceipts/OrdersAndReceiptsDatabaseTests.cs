using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Purchasing.Suppliers;
using ALKAROS.TestHelpers;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Purchasing.OrdersAndReceipts.Tests;

public sealed class OrdersAndReceiptsTestDb : PgTestDatabase
{
    public OrdersAndReceiptsTestDb() : base("alkaros_pur_orders_test_") { }

    protected override async Task ApplySqlAsync()
    {
        var sql059 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "059-stock-master.up.sql"));
        await RunAsync(DataSource, sql059);

        var sql060 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "060-stock-movements.up.sql"));
        await RunAsync(DataSource, sql060);

        var sql061 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "061-stock-balances.up.sql"));
        await RunAsync(DataSource, sql061);

        var sql068 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "068-suppliers.up.sql"));
        await RunAsync(DataSource, sql068);

        var sql069 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "069-purchase-orders-receipts.up.sql"));
        await RunAsync(DataSource, sql069);
    }

    public async Task RollbackMigration069Async()
    {
        var downSql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "069-purchase-orders-receipts.down.sql"));
        await RunAsync(DataSource, downSql);
    }

    public async Task ReapplyMigration069Async()
    {
        var sql069 = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", "069-purchase-orders-receipts.up.sql"));
        await RunAsync(DataSource, sql069);
    }
}

public sealed class OrdersAndReceiptsDatabaseTests : IClassFixture<OrdersAndReceiptsTestDb>
{
    private readonly OrdersAndReceiptsTestDb _db;
    private readonly PostgresPurchaseOrderRepository _poRepo;
    private readonly PostgresGoodsReceiptRepository _grRepo;
    private readonly PostgresSupplierRepository _supplierRepo;
    private readonly PurchasingService _service;

    public OrdersAndReceiptsDatabaseTests(OrdersAndReceiptsTestDb db)
    {
        _db = db;
        _poRepo = new PostgresPurchaseOrderRepository(db.DataSource);
        _grRepo = new PostgresGoodsReceiptRepository(db.DataSource);
        _supplierRepo = new PostgresSupplierRepository(db.DataSource);
        _service = new PurchasingService(
            _poRepo,
            _grRepo,
            _supplierRepo,
            db.DataSource,
            new PostgresStockBalanceRepository(db.DataSource),
            new PostgresStockMovementRepository(db.DataSource));
    }

    private async Task<(Guid supplierId, Guid locationId, Guid itemId)> SeedPrerequisitesAsync(bool supplierActive = true)
    {
        var supplier = Supplier.Create("SUP-" + Guid.NewGuid().ToString("N")[..8], "Test Supplier", active: supplierActive);
        await _supplierRepo.SaveAsync(supplier);

        var locationId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        await using var conn = await _db.DataSource.OpenConnectionAsync();
        const string locSql = @"
INSERT INTO inventory.stock_locations (id, code, name, location_type, is_active, row_version)
VALUES ($1, $2, 'Main Warehouse', 'Warehouse', true, 1);";
        await using var locCmd = new NpgsqlCommand(locSql, conn);
        locCmd.Parameters.AddWithValue(locationId);
        locCmd.Parameters.AddWithValue("LOC-" + Guid.NewGuid().ToString("N")[..8]);
        await locCmd.ExecuteNonQueryAsync();

        const string itemSql = @"
INSERT INTO inventory.stock_items (id, code, name, item_type, tracking_unit_code, is_active, row_version)
VALUES ($1, $2, 'Flour 50kg', 'RawMaterial', 'kg', true, 1);";
        await using var itemCmd = new NpgsqlCommand(itemSql, conn);
        itemCmd.Parameters.AddWithValue(itemId);
        itemCmd.Parameters.AddWithValue("SKU-" + Guid.NewGuid().ToString("N")[..8]);
        await itemCmd.ExecuteNonQueryAsync();

        return (supplier.Id, locationId, itemId);
    }

    [Fact]
    public async Task CreateAndSubmitPurchaseOrderPersistsHeaderAndLines()
    {
        var (supplierId, locationId, itemId) = await SeedPrerequisitesAsync();
        var poNum = "PO-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var po = await _service.CreatePurchaseOrderAsync(new CreatePOCommand(
            OrderNumber: poNum,
            SupplierId: supplierId,
            DestinationLocationId: locationId,
            Lines: new[] { new CreatePOLineDto(itemId, 50m, "kg", 12.50m) },
            Notes: "Monthly stock replenishment"));

        po.Should().NotBeNull();
        po.Lines.Should().HaveCount(1);
        po.Status.Should().Be(PurchaseOrderStatus.Draft);

        await _service.SubmitPurchaseOrderAsync(po.Id);

        var loaded = await _poRepo.GetByIdAsync(po.Id);
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be(PurchaseOrderStatus.Submitted);
        loaded.Lines.Should().HaveCount(1);
        loaded.Lines[0].OrderedQuantity.Should().Be(50m);
        loaded.Lines[0].ReceivedQuantity.Should().Be(0m);
    }

    [Fact]
    public async Task InactiveSupplierThrowsWhenCreatingPurchaseOrder()
    {
        var (supplierId, locationId, itemId) = await SeedPrerequisitesAsync(supplierActive: false);

        var act = async () => await _service.CreatePurchaseOrderAsync(new CreatePOCommand(
            OrderNumber: "PO-" + Guid.NewGuid().ToString("N")[..8],
            SupplierId: supplierId,
            DestinationLocationId: locationId,
            Lines: new[] { new CreatePOLineDto(itemId, 10m, "kg", 10m) }));

        await act.Should().ThrowAsync<InactiveSupplierException>();
    }

    [Fact]
    public async Task ReceiveGoodsExactDeliveryPostsStockMovementAndCompletesOrder()
    {
        var (supplierId, locationId, itemId) = await SeedPrerequisitesAsync();
        var po = await _service.CreatePurchaseOrderAsync(new CreatePOCommand(
            OrderNumber: "PO-" + Guid.NewGuid().ToString("N")[..8],
            SupplierId: supplierId,
            DestinationLocationId: locationId,
            Lines: new[] { new CreatePOLineDto(itemId, 100m, "kg", 20m) }));
        await _service.SubmitPurchaseOrderAsync(po.Id);

        var rcptNum = "GR-" + Guid.NewGuid().ToString("N")[..8];
        var receipt = await _service.ReceiveGoodsAsync(new ReceiveGoodsCommand(
            ReceiptNumber: rcptNum,
            OrderId: po.Id,
            ReceivedBy: "Warehouse Clerk",
            DeliveredItems: new[]
            {
                new ReceiveLineItemDto(po.Lines[0].Id, DeliveredQuantity: 100m)
            }));

        receipt.Should().NotBeNull();
        receipt.Items.Should().HaveCount(1);
        receipt.Items[0].AcceptedQuantity.Should().Be(100m);
        receipt.Items[0].RejectedQuantity.Should().Be(0m);

        // Verify PO is completed
        var updatedPo = await _poRepo.GetByIdAsync(po.Id);
        updatedPo!.Status.Should().Be(PurchaseOrderStatus.Completed);
        updatedPo.Lines[0].ReceivedQuantity.Should().Be(100m);
        updatedPo.Lines[0].Status.Should().Be(PurchaseOrderLineStatus.Completed);

        // Verify stock movement was recorded in inventory.stock_movements
        await using var conn = await _db.DataSource.OpenConnectionAsync();
        const string checkSql = @"
SELECT movement_type, quantity, direction
FROM inventory.stock_movements
WHERE source_reference_id = $1;";
        await using var cmd = new NpgsqlCommand(checkSql, conn);
        cmd.Parameters.AddWithValue(receipt.Id);

        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetString(0).Should().Be("PurchaseReceipt");
        reader.GetDecimal(1).Should().Be(100m);
        reader.GetString(2).Should().Be("In");
    }

    [Fact]
    public async Task PartialReceiptFollowedByRemainderCompletesOrder()
    {
        var (supplierId, locationId, itemId) = await SeedPrerequisitesAsync();
        var po = await _service.CreatePurchaseOrderAsync(new CreatePOCommand(
            OrderNumber: "PO-" + Guid.NewGuid().ToString("N")[..8],
            SupplierId: supplierId,
            DestinationLocationId: locationId,
            Lines: new[] { new CreatePOLineDto(itemId, 50m, "kg", 15m) }));
        await _service.SubmitPurchaseOrderAsync(po.Id);

        // 1st partial receipt: 20 units
        await _service.ReceiveGoodsAsync(new ReceiveGoodsCommand(
            ReceiptNumber: "GR-" + Guid.NewGuid().ToString("N")[..8],
            OrderId: po.Id,
            ReceivedBy: "Clerk",
            DeliveredItems: new[]
            {
                new ReceiveLineItemDto(po.Lines[0].Id, DeliveredQuantity: 20m, VarianceReason: "Partial delivery per truck load")
            }));

        var poMid = await _poRepo.GetByIdAsync(po.Id);
        poMid!.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);
        poMid.Lines[0].ReceivedQuantity.Should().Be(20m);
        poMid.Lines[0].Status.Should().Be(PurchaseOrderLineStatus.PartiallyReceived);

        // 2nd receipt: remaining 30 units
        await _service.ReceiveGoodsAsync(new ReceiveGoodsCommand(
            ReceiptNumber: "GR-" + Guid.NewGuid().ToString("N")[..8],
            OrderId: po.Id,
            ReceivedBy: "Clerk",
            DeliveredItems: new[]
            {
                new ReceiveLineItemDto(po.Lines[0].Id, DeliveredQuantity: 30m)
            }));

        var poFinal = await _poRepo.GetByIdAsync(po.Id);
        poFinal!.Status.Should().Be(PurchaseOrderStatus.Completed);
        poFinal.Lines[0].ReceivedQuantity.Should().Be(50m);
        poFinal.Lines[0].Status.Should().Be(PurchaseOrderLineStatus.Completed);
    }

    [Fact]
    public async Task AboveToleranceUnapprovedOverReceiptPostsOnlyOrderedAndRejectsExcess()
    {
        var (supplierId, locationId, itemId) = await SeedPrerequisitesAsync();
        var po = await _service.CreatePurchaseOrderAsync(new CreatePOCommand(
            OrderNumber: "PO-" + Guid.NewGuid().ToString("N")[..8],
            SupplierId: supplierId,
            DestinationLocationId: locationId,
            Lines: new[] { new CreatePOLineDto(itemId, 100m, "kg", 10m) }));
        await _service.SubmitPurchaseOrderAsync(po.Id);

        // Delivered 110 (10% excess > 5% tolerance), unapproved
        var receipt = await _service.ReceiveGoodsAsync(new ReceiveGoodsCommand(
            ReceiptNumber: "GR-" + Guid.NewGuid().ToString("N")[..8],
            OrderId: po.Id,
            ReceivedBy: "Clerk",
            DeliveredItems: new[]
            {
                new ReceiveLineItemDto(po.Lines[0].Id, DeliveredQuantity: 110m, VarianceReason: "Extra delivered by supplier")
            },
            IsManagerApproved: false));

        receipt.Items[0].AcceptedQuantity.Should().Be(100m);
        receipt.Items[0].RejectedQuantity.Should().Be(10m);

        // Check stock movement posted ONLY accepted quantity (100)
        await using var conn = await _db.DataSource.OpenConnectionAsync();
        const string checkSql = "SELECT quantity FROM inventory.stock_movements WHERE source_reference_id = $1;";
        await using var cmd = new NpgsqlCommand(checkSql, conn);
        cmd.Parameters.AddWithValue(receipt.Id);

        var postedQty = (decimal)(await cmd.ExecuteScalarAsync())!;
        postedQty.Should().Be(100m);
    }

    [Fact]
    public async Task AboveToleranceApprovedOverReceiptPostsFullQuantity()
    {
        var (supplierId, locationId, itemId) = await SeedPrerequisitesAsync();
        var po = await _service.CreatePurchaseOrderAsync(new CreatePOCommand(
            OrderNumber: "PO-" + Guid.NewGuid().ToString("N")[..8],
            SupplierId: supplierId,
            DestinationLocationId: locationId,
            Lines: new[] { new CreatePOLineDto(itemId, 100m, "kg", 10m) }));
        await _service.SubmitPurchaseOrderAsync(po.Id);

        // Delivered 110, manager approved
        var receipt = await _service.ReceiveGoodsAsync(new ReceiveGoodsCommand(
            ReceiptNumber: "GR-" + Guid.NewGuid().ToString("N")[..8],
            OrderId: po.Id,
            ReceivedBy: "Clerk",
            DeliveredItems: new[]
            {
                new ReceiveLineItemDto(po.Lines[0].Id, DeliveredQuantity: 110m, VarianceReason: "Special event extra prep")
            },
            IsManagerApproved: true,
            ApprovedBy: "General Manager"));

        receipt.Items[0].AcceptedQuantity.Should().Be(110m);
        receipt.Items[0].RejectedQuantity.Should().Be(0m);

        // Check stock movement posted full quantity (110)
        await using var conn2 = await _db.DataSource.OpenConnectionAsync();
        const string checkSql2 = "SELECT quantity FROM inventory.stock_movements WHERE source_reference_id = $1;";
        await using var cmd2 = new NpgsqlCommand(checkSql2, conn2);
        cmd2.Parameters.AddWithValue(receipt.Id);

        var postedQty = (decimal)(await cmd2.ExecuteScalarAsync())!;
        postedQty.Should().Be(110m);
    }

    [Fact]
    public async Task DuplicateReceiptNumberThrowsDuplicateGoodsReceiptException()
    {
        var (supplierId, locationId, itemId) = await SeedPrerequisitesAsync();
        var po = await _service.CreatePurchaseOrderAsync(new CreatePOCommand(
            OrderNumber: "PO-" + Guid.NewGuid().ToString("N")[..8],
            SupplierId: supplierId,
            DestinationLocationId: locationId,
            Lines: new[] { new CreatePOLineDto(itemId, 50m, "kg", 10m) }));
        await _service.SubmitPurchaseOrderAsync(po.Id);

        var rcptNum = "GR-DUP-" + Guid.NewGuid().ToString("N")[..8];
        await _service.ReceiveGoodsAsync(new ReceiveGoodsCommand(
            ReceiptNumber: rcptNum,
            OrderId: po.Id,
            ReceivedBy: "Clerk",
            DeliveredItems: new[] { new ReceiveLineItemDto(po.Lines[0].Id, DeliveredQuantity: 20m, VarianceReason: "Part 1") }));

        var actDuplicate = async () => await _service.ReceiveGoodsAsync(new ReceiveGoodsCommand(
            ReceiptNumber: rcptNum,
            OrderId: po.Id,
            ReceivedBy: "Clerk",
            DeliveredItems: new[] { new ReceiveLineItemDto(po.Lines[0].Id, DeliveredQuantity: 20m, VarianceReason: "Part 2") }));

        await actDuplicate.Should().ThrowAsync<DuplicateGoodsReceiptException>();
    }

    [Fact]
    public async Task Migration069RollbackAndReapplyWorksCleanly()
    {
        await _db.RollbackMigration069Async();
        await _db.ReapplyMigration069Async();

        var (supplierId, locationId, itemId) = await SeedPrerequisitesAsync();
        var po = await _service.CreatePurchaseOrderAsync(new CreatePOCommand(
            OrderNumber: "PO-" + Guid.NewGuid().ToString("N")[..8],
            SupplierId: supplierId,
            DestinationLocationId: locationId,
            Lines: new[] { new CreatePOLineDto(itemId, 10m, "kg", 5m) }));

        po.Should().NotBeNull();
    }
}
