using System.Net;
using ALKAROS.Host.Experience.Orders.OrderStockConsumption;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.ManualAdjustments;
using ALKAROS.Inventory.PhysicalCounts;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Inventory.WasteRecording;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Production.BatchLifecycle;
using ALKAROS.Production.StockEffects;
using ALKAROS.Recipes.CatalogMapping;
using ALKAROS.Recipes.Versioning;
using ALKAROS.Reporting.MenuInventory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Audit.StockFlowProbes;

/// <summary>
/// V1-RMD-398 stock/recipe flow probes. Each asserts what the system MUST do; a failing probe is an audit
/// finding, a passing probe closes its matrix cell as verified-sound. Stock state is produced through the real
/// services (adjustment, consumption, count, waste, production) so ledger and balance stay the system's own.
/// </summary>
public sealed class StockFlowProbes : IClassFixture<ProbeHarness>
{
    private readonly ProbeHarness _h;

    public StockFlowProbes(ProbeHarness harness) => _h = harness;

    private static T Service<T>(AsyncServiceScope scope) where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    private static string Code(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N")[..10];

    private async Task<(Guid ItemId, Guid LocationId)> SeedStockAsync(decimal onHand, string unit = "kg", Guid? locationId = null)
    {
        await using var scope = _h.App.Services.CreateAsyncScope();
        var master = Service<IStockMasterService>(scope);
        var location = locationId ?? (await master.CreateLocationAsync(Code("LOC"), "Probe Depo", StockLocationType.Warehouse)).Id;
        var item = await master.CreateStockItemAsync(Code("ITM"), "Probe Malzeme", StockItemType.RawMaterial, unit, location);
        if (onHand > 0)
            await AdjustAsync(item.Id, location, AdjustmentDirection.Increase, onHand, unit);
        return (item.Id, location);
    }

    private async Task AdjustAsync(Guid itemId, Guid locationId, AdjustmentDirection direction, decimal quantity, string unit)
    {
        await using var scope = _h.App.Services.CreateAsyncScope();
        await Service<IInventoryAdjustmentService>(scope).AdjustInventoryAsync(new InventoryAdjustmentRequest(
            itemId, locationId, direction, quantity, unit, "Probe seed", Guid.NewGuid(), Code("adj")));
    }

    private Task<decimal> OnHandAsync(Guid itemId, Guid locationId) => _h.ScalarAsync<decimal>(
        "SELECT on_hand_quantity FROM inventory.stock_balances WHERE stock_item_id = @i AND stock_location_id = @l;",
        ("i", itemId), ("l", locationId));

    private Task<decimal> LedgerSumAsync(Guid itemId, Guid locationId) => _h.ScalarAsync<decimal>(
        """
        SELECT COALESCE(SUM(CASE WHEN direction = 'In' THEN quantity ELSE -quantity END), 0)
        FROM inventory.stock_movements WHERE stock_item_id = @i AND stock_location_id = @l;
        """, ("i", itemId), ("l", locationId));

    private async Task<Guid> SeedProductAsync()
    {
        var productId = Guid.NewGuid();
        await _h.ScalarAsync<int>(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, current_price)
            VALUES (@p, @sku, 'Probe Urun', 1, 2, 100) RETURNING 1;
            """, ("p", productId), ("sku", Code("SKU")));
        return productId;
    }

    private async Task<(Guid RecipeId, Guid VersionId)> SeedActiveRecipeAsync(
        decimal yieldQuantity, string yieldUnit, params (Guid Item, decimal Qty, string Unit, decimal LossPct)[] ingredients)
    {
        await using var scope = _h.App.Services.CreateAsyncScope();
        var lifecycle = Service<IRecipeLifecycleService>(scope);
        var recipe = await lifecycle.CreateRecipeAsync(Code("RCP"), "Probe Recete");
        var version = await lifecycle.CreateInitialDraftVersionAsync(recipe.Id, yieldQuantity, yieldUnit);
        var order = 0;
        foreach (var (item, qty, unit, loss) in ingredients)
            version = await lifecycle.AddIngredientToDraftAsync(version.Id, item, qty, unit, loss, order++);
        await lifecycle.ActivateVersionAsync(recipe.Id, version.VersionNumber);
        return (recipe.Id, version.Id);
    }

    private async Task<Order> SeedSubmittedOrderAsync(Guid servingUserId, params (Guid ProductId, decimal Qty, KitchenState Kitchen)[] lines)
    {
        var orderId = Guid.NewGuid();
        var items = lines.Select(l => new OrderItem(
            Guid.NewGuid(), orderId, l.ProductId, "Probe Urun", l.Qty, 100m, 0m,
            status: OrderItemState.Active, kitchenState: l.Kitchen)).ToArray();
        var order = new Order(orderId, OrderSource.Cashier, Code("ORD"), items, status: OrderState.Submitted, servingUserId: servingUserId);
        await new PostgresOrderRepository(_h.DataSource).AddAsync(order);
        return order;
    }

    private async Task ConsumeAsync(Order order, Guid actorId)
    {
        await using var scope = _h.App.Services.CreateAsyncScope();
        var consumption = Service<OrderStockConsumptionService>(scope);
        await using var connection = await _h.DataSource.OpenConnectionAsync();
        await using var tx = await connection.BeginTransactionAsync();
        await consumption.ConsumeItemsAsync(order, order.Items, "probe", actorId, connection, tx);
        await tx.CommitAsync();
    }

    // S01 - a physical count SETS on-hand to the counted quantity (cash-session-like "count" semantics,
    // PhysicalCountResult.NewOnHandQuantity). A stock write landing between the service's balance read and its
    // transaction must not make the count's own result differ from what was counted.
    [Fact]
    public async Task S01PhysicalCountResultEqualsTheCountedQuantityUnderAConcurrentWrite()
    {
        var (item, loc) = await SeedStockAsync(1000m);
        var violations = new List<string>();
        for (var i = 0; i < 40; i++)
        {
            var counted = 900m - i;
            var countTask = Task.Run(async () =>
            {
                await using var scope = _h.App.Services.CreateAsyncScope();
                return await Service<IPhysicalCountService>(scope).RecordPhysicalCountAsync(
                    new PhysicalCountRequest(item, loc, counted, Guid.NewGuid(), "probe"));
            });
            var writeTask = Task.Run(() => AdjustAsync(item, loc, AdjustmentDirection.Decrease, 1m, "kg"));
            var result = await countTask;
            await writeTask;
            if (result.NewOnHandQuantity != counted)
                violations.Add($"counted {counted}, count result on-hand {result.NewOnHandQuantity} (read {result.PreviousOnHandQuantity})");
        }

        Assert.True(violations.Count == 0, $"{violations.Count}/40 counts did not land on the counted quantity: " + string.Join("; ", violations.Take(5)));
    }

    // S02 - voiding a sent item gives its stock back AND takes back its theoretical consumption; otherwise the
    // actual-vs-theoretical report counts a dish nobody ate as expected usage.
    [Fact]
    public async Task S02VoidingASentItemAlsoWithdrawsItsTheoreticalConsumption()
    {
        var terminalId = Guid.NewGuid();
        var (actor, cookie) = await _h.SeedCashierAsync(terminalId, "bills.void");
        var (item, loc) = await SeedStockAsync(50m);
        var product = await SeedProductAsync();
        await using (var scope = _h.App.Services.CreateAsyncScope())
        {
            await Service<IStockMasterService>(scope).AssignProductToStockItemAsync(product, item, 0.2m);
            var (recipeId, _) = await SeedActiveRecipeAsync(1m, "portion", (item, 0.2m, "kg", 0m));
            await Service<IProductRecipeMappingRepository>(scope).AddOrUpdateAsync(new ProductRecipeMapping(product, recipeId));
        }
        var order = await SeedSubmittedOrderAsync(actor, (product, 2m, KitchenState.Sent));
        await ConsumeAsync(order, actor);
        var line = order.Items[0];
        Assert.Equal(49.6m, await OnHandAsync(item, loc));
        var theoreticalBefore = await _h.ScalarAsync<decimal>(
            "SELECT COALESCE(SUM(quantity), 0) FROM recipe.theoretical_consumption_records WHERE order_item_id = @id;", ("id", line.Id));
        Assert.Equal(0.4m, theoreticalBefore);

        var rowVersion = await _h.ScalarAsync<long>("SELECT row_version FROM orders.orders WHERE order_id = @id;", ("id", order.Id));
        var voided = await _h.PostAsync($"/api/v1/terminals/{terminalId:D}/orders/{order.Id:D}/items/{line.Id:D}/void-sent", cookie,
            new { IdempotencyKey = Code("void"), ExpectedRowVersion = rowVersion, ReasonCode = "CustomerChange" });
        Assert.Equal(HttpStatusCode.OK, voided.StatusCode);
        Assert.Equal(50m, await OnHandAsync(item, loc)); // stock restored (control)

        var theoreticalAfter = await _h.ScalarAsync<decimal>(
            "SELECT COALESCE(SUM(quantity), 0) FROM recipe.theoretical_consumption_records WHERE order_item_id = @id;", ("id", line.Id));
        Assert.True(theoreticalAfter == 0m,
            $"Voided item still carries {theoreticalAfter} kg of theoretical consumption; the actual-vs-theoretical report will show a false shortage of that amount.");
    }

    // S03 - theoretical usage of a stock item is reported once per location it was used from, not once per
    // location it merely has a count in.
    [Fact]
    public async Task S03ActualVsTheoreticalDoesNotRepeatTheoreticalUsageForEveryCountedLocation()
    {
        var (item, locA) = await SeedStockAsync(10m);
        Guid locB;
        await using (var scope = _h.App.Services.CreateAsyncScope())
            locB = (await Service<IStockMasterService>(scope).CreateLocationAsync(Code("LOC"), "Probe Depo B", StockLocationType.Warehouse)).Id;
        await using (var scope = _h.App.Services.CreateAsyncScope())
        {
            var counts = Service<IPhysicalCountService>(scope);
            await counts.RecordPhysicalCountAsync(new PhysicalCountRequest(item, locA, 10m, Guid.NewGuid(), "open A"));
            await counts.RecordPhysicalCountAsync(new PhysicalCountRequest(item, locB, 0m, Guid.NewGuid(), "open B"));
        }
        await Task.Delay(50);
        var from = DateTimeOffset.UtcNow;
        await Task.Delay(50);
        var (recipeId, versionId) = await SeedActiveRecipeAsync(1m, "portion", (item, 3m, "kg", 0m));
        await _h.ScalarAsync<int>(
            """
            INSERT INTO recipe.theoretical_consumption_records (id, order_item_id, product_id, recipe_id, recipe_version_id, stock_item_id, quantity, unit_code, recorded_at)
            VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), @r, @v, @i, 3, 'kg', now()) RETURNING 1;
            """, ("r", recipeId), ("v", versionId), ("i", item));
        await AdjustAsync(item, locA, AdjustmentDirection.Decrease, 3m, "kg");
        await using (var scope = _h.App.Services.CreateAsyncScope())
        {
            var counts = Service<IPhysicalCountService>(scope);
            await counts.RecordPhysicalCountAsync(new PhysicalCountRequest(item, locA, 7m, Guid.NewGuid(), "close A"));
            await counts.RecordPhysicalCountAsync(new PhysicalCountRequest(item, locB, 0m, Guid.NewGuid(), "close B"));
        }

        ActualVsTheoreticalReport report;
        await using (var scope = _h.App.Services.CreateAsyncScope())
            report = await Service<IMenuInventoryReportingService>(scope).GetActualVsTheoreticalReportAsync(
                new ActualVsTheoreticalReportQuery(from, DateTimeOffset.UtcNow.AddSeconds(1)));
        var rows = report.Items.Where(r => r.StockItemId == item).ToList();
        var theoreticalTotal = rows.Sum(r => r.TheoreticalUsage);
        Assert.True(theoreticalTotal == 3m,
            $"3 kg of theoretical usage is reported as {theoreticalTotal} kg across {rows.Count} location rows " +
            $"({string.Join(", ", rows.Select(r => $"{r.LocationName}: actual {r.ActualUsage}, theoretical {r.TheoreticalUsage}, variance {r.VarianceQuantity}"))}).");
    }

    // S04 - a production batch whose quantity unit is not the recipe's yield unit must be converted or refused,
    // never silently scaled as if the units were the same.
    [Fact]
    public async Task S04ProductionRefusesOrConvertsABatchUnitThatDiffersFromTheRecipeYieldUnit()
    {
        var (flour, loc) = await SeedStockAsync(100m);
        var (_, versionId) = await SeedActiveRecipeAsync(2m, "kg", (flour, 1m, "kg", 0m)); // 1 kg flour per 2 kg of dough
        Guid batchId;
        await using (var scope = _h.App.Services.CreateAsyncScope())
        {
            var batches = Service<IProductionBatchService>(scope);
            var batch = await batches.CreateBatchAsync(new CreateProductionBatchCommand(Code("B"), versionId, 4m)); // PortionUnitCode defaults to "portion"
            batchId = batch.Id;
            await batches.StartBatchAsync(new StartProductionBatchCommand(batchId));
        }

        Exception? refused = null;
        try
        {
            await using var scope = _h.App.Services.CreateAsyncScope();
            await Service<IProductionStockEffectService>(scope).ExecuteBatchStockEffectsAsync(
                new ExecuteBatchStockEffectsCommand(batchId, 4m, loc, null, null, ExecutedBy: Guid.NewGuid()));
        }
        catch (Exception exception)
        {
            refused = exception;
        }

        var consumed = 100m - await OnHandAsync(flour, loc);
        Assert.True(refused is not null,
            $"A 4 'portion' batch of a recipe whose yield is 2 kg was executed without any unit check and consumed {consumed} kg of flour " +
            "(scale = 4 / 2 with the units ignored).");
    }

    // S05 - flour consumed by a documented production batch is explained usage; the actual-vs-theoretical report
    // must not present it as unexplained variance.
    [Fact]
    public async Task S05ProductionConsumptionIsNotReportedAsUnexplainedVariance()
    {
        var (flour, loc) = await SeedStockAsync(20m);
        var (_, versionId) = await SeedActiveRecipeAsync(1m, "portion", (flour, 0.5m, "kg", 0m));
        await using (var scope = _h.App.Services.CreateAsyncScope())
            await Service<IPhysicalCountService>(scope).RecordPhysicalCountAsync(new PhysicalCountRequest(flour, loc, 20m, Guid.NewGuid(), "open"));
        await Task.Delay(50);
        var from = DateTimeOffset.UtcNow;
        await Task.Delay(50);
        await using (var scope = _h.App.Services.CreateAsyncScope())
        {
            var batches = Service<IProductionBatchService>(scope);
            var batch = await batches.CreateBatchAsync(new CreateProductionBatchCommand(Code("B"), versionId, 10m));
            await batches.StartBatchAsync(new StartProductionBatchCommand(batch.Id));
            await Service<IProductionStockEffectService>(scope).ExecuteBatchStockEffectsAsync(
                new ExecuteBatchStockEffectsCommand(batch.Id, 10m, loc, null, null, ExecutedBy: Guid.NewGuid()));
        }
        Assert.Equal(15m, await OnHandAsync(flour, loc));
        await using (var scope = _h.App.Services.CreateAsyncScope())
            await Service<IPhysicalCountService>(scope).RecordPhysicalCountAsync(new PhysicalCountRequest(flour, loc, 15m, Guid.NewGuid(), "close"));

        ActualVsTheoreticalReport report;
        await using (var scope = _h.App.Services.CreateAsyncScope())
            report = await Service<IMenuInventoryReportingService>(scope).GetActualVsTheoreticalReportAsync(
                new ActualVsTheoreticalReportQuery(from, DateTimeOffset.UtcNow.AddSeconds(1)));
        var row = Assert.Single(report.Items, r => r.StockItemId == flour);
        Assert.True(row.VarianceQuantity == 0m,
            $"5 kg of flour used by a completed production batch is reported as variance {row.VarianceQuantity} kg " +
            $"(actual {row.ActualUsage}, theoretical {row.TheoreticalUsage}) - production is not part of the report's formula.");
    }

    // S06 - a sale whose recipe ingredient unit cannot be converted to the stock item's unit must not silently
    // drop that ingredient's theoretical consumption (production refuses the same situation outright).
    [Fact]
    public async Task S06UnconvertibleRecipeUnitDoesNotSilentlyDropTheoreticalConsumption()
    {
        var actor = Guid.NewGuid();
        var (sold, _) = await SeedStockAsync(100m, "adet");
        var (sauce, _) = await SeedStockAsync(100m, "kg");
        var product = await SeedProductAsync();
        await using (var scope = _h.App.Services.CreateAsyncScope())
        {
            await Service<IStockMasterService>(scope).AssignProductToStockItemAsync(product, sold, 1m);
            var (recipeId, _) = await SeedActiveRecipeAsync(1m, "portion", (sauce, 2m, "adet", 0m));
            await Service<IProductRecipeMappingRepository>(scope).AddOrUpdateAsync(new ProductRecipeMapping(product, recipeId));
        }
        var order = await SeedSubmittedOrderAsync(actor, (product, 1m, KitchenState.Sent));

        Exception? refused = null;
        try { await ConsumeAsync(order, actor); }
        catch (Exception exception) { refused = exception; }

        var recorded = await _h.ScalarAsync<long>(
            "SELECT count(*) FROM recipe.theoretical_consumption_records WHERE order_item_id = @id;", ("id", order.Items[0].Id));
        Assert.True(refused is not null || recorded > 0,
            "The sale succeeded and recorded no theoretical consumption for the 'adet' -> 'kg' ingredient; no error, no log entry, no record.");
    }

    // S07 - CONTROL: the last 5 units of a stock item cannot be oversold by 10 racing orders.
    [Fact]
    public async Task S07ControlRacingOrdersNeverOversellTheLastUnits()
    {
        var actor = Guid.NewGuid();
        var (item, loc) = await SeedStockAsync(5m, "adet");
        var product = await SeedProductAsync();
        await using (var scope = _h.App.Services.CreateAsyncScope())
            await Service<IStockMasterService>(scope).AssignProductToStockItemAsync(product, item, 1m);
        var orders = new List<Order>();
        for (var i = 0; i < 10; i++)
            orders.Add(await SeedSubmittedOrderAsync(actor, (product, 1m, KitchenState.Sent)));

        var results = await Task.WhenAll(orders.Select(async o =>
        {
            try { await ConsumeAsync(o, actor); return true; }
            catch (InsufficientOrderStockException) { return false; }
        }));

        Assert.Equal(5, results.Count(ok => ok));
        Assert.Equal(0m, await OnHandAsync(item, loc));
        Assert.Equal(await LedgerSumAsync(item, loc), await OnHandAsync(item, loc));
    }

    // S08 - a retried waste record / manual adjustment with the same idempotency key writes exactly one movement.
    [Fact]
    public async Task S08WasteAndAdjustmentRetriesWithTheSameKeyApplyOnce()
    {
        var (item, loc) = await SeedStockAsync(30m);
        var wasteKey = Code("waste");
        var adjustKey = Code("adj");
        await using var scope = _h.App.Services.CreateAsyncScope();
        var waste = Service<IWasteRecordingService>(scope);
        var adjust = Service<IInventoryAdjustmentService>(scope);
        var actor = Guid.NewGuid();
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(async () =>
        {
            await using var inner = _h.App.Services.CreateAsyncScope();
            await Service<IWasteRecordingService>(inner).RecordWasteAsync(
                new RecordWasteRequest(item, loc, WasteSources.Spoilage, 2m, "kg", "Spoilage", actor, IdempotencyKey: wasteKey));
        })));
        var afterWaste = await OnHandAsync(item, loc);
        await adjust.AdjustInventoryAsync(new InventoryAdjustmentRequest(item, loc, AdjustmentDirection.Decrease, 1m, "kg", "probe", actor, adjustKey));
        await adjust.AdjustInventoryAsync(new InventoryAdjustmentRequest(item, loc, AdjustmentDirection.Decrease, 1m, "kg", "probe", actor, adjustKey));
        var afterAdjust = await OnHandAsync(item, loc);

        Assert.True(afterWaste == 28m && afterAdjust == 27m,
            $"5 concurrent waste submits (same key, 2 kg) left {afterWaste} (expected 28); two sequential adjustments " +
            $"(same key, 1 kg) then left {afterAdjust} (expected 27).");
        Assert.Equal(await LedgerSumAsync(item, loc), await OnHandAsync(item, loc));
    }

    // S09 - completing the same production batch twice consumes its ingredients once.
    [Fact]
    public async Task S09CompletingAProductionBatchTwiceConsumesOnce()
    {
        var (flour, loc) = await SeedStockAsync(10m);
        var (_, versionId) = await SeedActiveRecipeAsync(1m, "portion", (flour, 1m, "kg", 0m));
        await using var scope = _h.App.Services.CreateAsyncScope();
        var batches = Service<IProductionBatchService>(scope);
        var batch = await batches.CreateBatchAsync(new CreateProductionBatchCommand(Code("B"), versionId, 3m));
        await batches.StartBatchAsync(new StartProductionBatchCommand(batch.Id));
        var effects = Service<IProductionStockEffectService>(scope);
        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Task.Run(async () =>
        {
            await using var inner = _h.App.Services.CreateAsyncScope();
            try
            {
                await Service<IProductionStockEffectService>(inner).ExecuteBatchStockEffectsAsync(
                    new ExecuteBatchStockEffectsCommand(batch.Id, 3m, loc, null, null, ExecutedBy: Guid.NewGuid()));
            }
            catch (InvalidProductionStockEffectException)
            {
                // A concurrent loser refusing is acceptable; double consumption is not.
            }
        })));

        Assert.Equal(7m, await OnHandAsync(flour, loc));
        Assert.Equal(await LedgerSumAsync(flour, loc), await OnHandAsync(flour, loc));
    }

    // S10 - loss factor (C9: multiply in the recipe's native unit, then convert) is applied to production.
    [Fact]
    public async Task S10ProductionAppliesLossBeforeUnitConversion()
    {
        var (flour, loc) = await SeedStockAsync(10m, "kg");
        var (_, versionId) = await SeedActiveRecipeAsync(1m, "portion", (flour, 500m, "g", 10m)); // 500 g + 10% loss
        await using var scope = _h.App.Services.CreateAsyncScope();
        var batches = Service<IProductionBatchService>(scope);
        var batch = await batches.CreateBatchAsync(new CreateProductionBatchCommand(Code("B"), versionId, 4m));
        await batches.StartBatchAsync(new StartProductionBatchCommand(batch.Id));
        await Service<IProductionStockEffectService>(scope).ExecuteBatchStockEffectsAsync(
            new ExecuteBatchStockEffectsCommand(batch.Id, 4m, loc, null, null, ExecutedBy: Guid.NewGuid()));

        Assert.Equal(10m - 2.2m, await OnHandAsync(flour, loc)); // 4 x 500 g x 1.10 = 2200 g = 2.2 kg
    }

    // Receives `quantity` of `item` at `unitPrice` through the real purchasing chain and returns the receipt id.
    private async Task<Guid> ReceiveAsync(Guid item, Guid location, decimal quantity, decimal unitPrice)
    {
        await using var scope = _h.App.Services.CreateAsyncScope();
        var supplier = await Service<ALKAROS.Purchasing.Suppliers.ISupplierService>(scope).CreateSupplierAsync(
            new ALKAROS.Purchasing.Suppliers.CreateSupplierCommand(Code("SUP"), "Probe Tedarikci"));
        var purchasing = Service<ALKAROS.Purchasing.OrdersAndReceipts.IPurchasingService>(scope);
        var po = await purchasing.CreatePurchaseOrderAsync(new ALKAROS.Purchasing.OrdersAndReceipts.CreatePOCommand(
            Code("PO"), supplier.Id, location, [new ALKAROS.Purchasing.OrdersAndReceipts.CreatePOLineDto(item, quantity, "kg", unitPrice)]));
        await purchasing.SubmitPurchaseOrderAsync(po.Id);
        var receipt = await purchasing.ReceiveGoodsAsync(new ALKAROS.Purchasing.OrdersAndReceipts.ReceiveGoodsCommand(
            Code("GR"), po.Id, "probe", [new ALKAROS.Purchasing.OrdersAndReceipts.ReceiveLineItemDto(po.Lines[0].Id, quantity)]));
        return receipt.Id;
    }

    private async Task<decimal?> MovingAverageAsync(Guid item, DateOnly asOf)
    {
        await using var scope = _h.App.Services.CreateAsyncScope();
        return await Service<ALKAROS.Recipes.CostSnapshots.IStockCostResolver>(scope).ResolveMovingAverageCostAsync(item, asOf);
    }

    // S12 - BOUNDARY (added after the blind calibration run: no cost probe existed although cost snapshots are in
    // scope). The moving-average cost "as of" a date includes goods received ON that date.
    [Fact]
    public async Task S12MovingAverageCostIncludesReceiptsOnTheCostBasisDate()
    {
        var (item, loc) = await SeedStockAsync(0m);
        var older = await ReceiveAsync(item, loc, 10m, 30m);
        await _h.ScalarAsync<int>("UPDATE purchasing.goods_receipts SET received_at = received_at - interval '3 days' WHERE receipt_id = @id RETURNING 1;", ("id", older));
        await ReceiveAsync(item, loc, 10m, 50m);
        var today = await _h.ScalarAsync<DateTime>("SELECT CAST(now() AS date);");

        var cost = await MovingAverageAsync(item, DateOnly.FromDateTime(today));

        Assert.True(cost == 40m, $"Moving-average cost as of the day of the 50 TL receipt is {cost?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"}; expected 40 (both receipts).");
    }

    // S13 - TIME ZONE: goods received at 01:30 Istanbul time on day D+1 belong to D+1 and must not change the
    // moving-average cost "as of" D. The resolver casts timestamptz to date in the database session zone; nothing
    // in the deployment sets it, so 01:30 local (22:30 UTC on D) is counted in D.
    [Fact]
    public async Task S13MovingAverageCostUsesTheRestaurantsLocalDateNotUtc()
    {
        var (item, loc) = await SeedStockAsync(0m);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        var dayD = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime).AddDays(-3);
        DateTimeOffset LocalToUtc(DateOnly day, int hour, int minute)
        {
            var local = day.ToDateTime(new TimeOnly(hour, minute));
            return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
        }

        var noonOfD = await ReceiveAsync(item, loc, 10m, 30m);
        await _h.ScalarAsync<int>("UPDATE purchasing.goods_receipts SET received_at = @at WHERE receipt_id = @id RETURNING 1;",
            ("at", LocalToUtc(dayD, 12, 0)), ("id", noonOfD));
        var earlyNextDay = await ReceiveAsync(item, loc, 10m, 90m);
        await _h.ScalarAsync<int>("UPDATE purchasing.goods_receipts SET received_at = @at WHERE receipt_id = @id RETURNING 1;",
            ("at", LocalToUtc(dayD.AddDays(1), 1, 30)), ("id", earlyNextDay));

        var cost = await MovingAverageAsync(item, dayD);

        Assert.True(cost == 30m,
            $"Moving-average cost as of {dayD:yyyy-MM-dd} is {cost?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"}; " +
            $"expected 30 - the 90 TL receipt at 01:30 Istanbul on {dayD.AddDays(1):yyyy-MM-dd} was counted in the previous day (UTC date).");
    }

    // S11 - CARDINALITY: the same product on three lines plus a second mapped stock item consume the sum of
    // every line x multiplier, and a void of one line restores exactly that line.
    [Fact]
    public async Task S11MultipleLinesAndMappingsConsumeAndRestoreExactly()
    {
        var terminalId = Guid.NewGuid();
        var (actor, cookie) = await _h.SeedCashierAsync(terminalId, "bills.void");
        var (bun, loc) = await SeedStockAsync(100m, "adet");
        var (meat, _) = await SeedStockAsync(20m, "kg", loc);
        var product = await SeedProductAsync();
        await using (var scope = _h.App.Services.CreateAsyncScope())
        {
            var master = Service<IStockMasterService>(scope);
            await master.AssignProductToStockItemAsync(product, bun, 1m);
            await master.AssignProductToStockItemAsync(product, meat, 0.15m);
        }
        var order = await SeedSubmittedOrderAsync(actor,
            (product, 2m, KitchenState.Sent), (product, 3m, KitchenState.Sent), (product, 1m, KitchenState.Sent));
        await ConsumeAsync(order, actor);
        Assert.Equal(94m, await OnHandAsync(bun, loc));
        Assert.Equal(19.1m, await OnHandAsync(meat, loc));

        var line = order.Items[1];
        var rowVersion = await _h.ScalarAsync<long>("SELECT row_version FROM orders.orders WHERE order_id = @id;", ("id", order.Id));
        var voided = await _h.PostAsync($"/api/v1/terminals/{terminalId:D}/orders/{order.Id:D}/items/{line.Id:D}/void-sent", cookie,
            new { IdempotencyKey = Code("void"), ExpectedRowVersion = rowVersion, ReasonCode = "CustomerChange" });
        Assert.Equal(HttpStatusCode.OK, voided.StatusCode);

        Assert.Equal(97m, await OnHandAsync(bun, loc));
        Assert.Equal(19.55m, await OnHandAsync(meat, loc));
        Assert.Equal(await LedgerSumAsync(bun, loc), await OnHandAsync(bun, loc));
        Assert.Equal(await LedgerSumAsync(meat, loc), await OnHandAsync(meat, loc));
    }
}
