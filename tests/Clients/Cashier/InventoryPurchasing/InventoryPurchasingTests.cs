using FluentAssertions;
using Xunit;

namespace ALKAROS.Clients.Cashier.InventoryPurchasing.Tests;

public sealed class InventoryPurchasingTests
{
    private readonly InventoryPurchasingEngine _engine = new();
    private readonly InventoryPurchasingOperator _adminUser;
    private readonly InventoryPurchasingOperator _unauthorizedUser;

    private readonly Guid _stockItemIdFlour = Guid.NewGuid();
    private readonly Guid _stockItemIdSugar = Guid.NewGuid();
    private readonly Guid _mainWarehouseId = Guid.NewGuid();
    private readonly Guid _kitchenLocationId = Guid.NewGuid();
    private readonly Guid _purchaseOrderId = Guid.NewGuid();

    public InventoryPurchasingTests()
    {
        _adminUser = new InventoryPurchasingOperator(
            OperatorId: Guid.NewGuid(),
            FullName: "Depo Sorumlusu Hasan",
            Permissions: new HashSet<string>
            {
                InvPurchPermissions.InventoryAdjust,
                InvPurchPermissions.PurchasingReceive
            });

        _unauthorizedUser = new InventoryPurchasingOperator(
            OperatorId: Guid.NewGuid(),
            FullName: "Stajyer Garson",
            Permissions: new HashSet<string>());

        var initialBalances = new List<StockBalanceView>
        {
            new(
                StockItemId: _stockItemIdFlour,
                ItemCode: "STK-UN-01",
                ItemName: "Ekmeklik Un",
                LocationId: _mainWarehouseId,
                LocationName: "Ana Depo",
                OnHandBalance: 100.00m,
                ReservedBalance: 20.00m,
                AvailableBalance: 80.00m,
                UnitCode: "kg",
                RowVersion: 1,
                LastProjectedAt: DateTimeOffset.UtcNow),

            new(
                StockItemId: _stockItemIdSugar,
                ItemCode: "STK-SEKER-01",
                ItemName: "Toz Şeker",
                LocationId: _mainWarehouseId,
                LocationName: "Ana Depo",
                OnHandBalance: 50.00m,
                ReservedBalance: 0.00m,
                AvailableBalance: 50.00m,
                UnitCode: "kg",
                RowVersion: 1,
                LastProjectedAt: DateTimeOffset.UtcNow)
        };

        _engine.LoadStockBalances(initialBalances);

        var po = new PurchaseOrderView(
            OrderId: _purchaseOrderId,
            OrderNumber: "PO-2026-001",
            SupplierId: Guid.NewGuid(),
            SupplierName: "Bereket Gıda Toptan",
            Status: "Approved",
            RowVersion: 1,
            Lines: new List<PurchaseOrderItemView>
            {
                new(Guid.NewGuid(), _stockItemIdFlour, "Ekmeklik Un", 50.00m, 0m, "kg", 25.00m)
            });

        _engine.LoadPurchaseOrders(new[] { po });
    }

    [Fact]
    public void DirectStockBalanceMutationIsProhibited()
    {
        _engine.SetOperator(_adminUser);

        // ACCEPTANCE CRITERION 1: "UI asla bakiyeyi doğrudan yazmaz"
        var directCmd = new AttemptDirectBalanceMutationCommand(
            StockItemId: _stockItemIdFlour,
            LocationId: _mainWarehouseId,
            ArbitraryBalance: 999.00m);

        var result = _engine.AttemptDirectBalanceMutation(directCmd);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("DIRECT_BALANCE_MUTATION_PROHIBITED");
        result.ErrorMessage.Should().Be("Stok bakiyeleri doğrudan düzenlenemez; yalnızca denetimli hareketler (alım, ayarlama, atık) ile güncellenebilir.");

        // Balance remains unchanged
        var balance = _engine.GetStockBalance(_stockItemIdFlour, _mainWarehouseId);
        balance.Should().NotBeNull();
        balance!.OnHandBalance.Should().Be(100.00m);
    }

    [Fact]
    public void DuplicateGoodsReceiptSubmissionsHaveSingleServerEffectIdempotently()
    {
        _engine.SetOperator(_adminUser);

        var idempotencyKey = "KEY-RCP-001";
        var receiveCmd = new ReceiveGoodsCommand(
            PurchaseOrderId: _purchaseOrderId,
            ReceiptNumber: "IRS-2026-8874",
            LocationId: _mainWarehouseId,
            Lines: new List<ReceiveGoodsLineInput>
            {
                new(_stockItemIdFlour, 50.00m, 25.00m)
            },
            IdempotencyKey: idempotencyKey,
            ExpectedOrderRowVersion: 1);

        // First execution
        var result1 = _engine.ReceiveGoods(receiveCmd);
        result1.Success.Should().BeTrue();
        _engine.GoodsReceiptMovementCount.Should().Be(1);

        var bal1 = _engine.GetStockBalance(_stockItemIdFlour, _mainWarehouseId);
        bal1!.OnHandBalance.Should().Be(150.00m); // 100 + 50

        // Duplicate execution with same idempotency key
        var result2 = _engine.ReceiveGoods(receiveCmd);
        result2.Success.Should().BeTrue();

        // ACCEPTANCE CRITERION 2: "tekrarlanan alım/ayarlama eyleminin tek bir sunucu etkisi vardır"
        _engine.GoodsReceiptMovementCount.Should().Be(1, "Repeated goods receipt must have only single server effect");
        var bal2 = _engine.GetStockBalance(_stockItemIdFlour, _mainWarehouseId);
        bal2!.OnHandBalance.Should().Be(150.00m, "Stock balance must not be credited twice");
    }

    [Fact]
    public void StaleRowVersionGoodsReceiptIsRejected()
    {
        _engine.SetOperator(_adminUser);

        // Order is at row version 1, client sends stale row version 0
        var staleCmd = new ReceiveGoodsCommand(
            PurchaseOrderId: _purchaseOrderId,
            ReceiptNumber: "IRS-2026-9999",
            LocationId: _mainWarehouseId,
            Lines: new List<ReceiveGoodsLineInput>
            {
                new(_stockItemIdFlour, 20.00m, 25.00m)
            },
            IdempotencyKey: "KEY-STALE-RCP",
            ExpectedOrderRowVersion: 99); // Stale version!

        var result = _engine.ReceiveGoods(staleCmd);

        // ACCEPTANCE CRITERION 3: "eski satır reddedildi"
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("CONCURRENCY_CONFLICT");
        result.ErrorMessage.Should().Contain("Kayıt başka bir kullanıcı veya terminal tarafından güncellendi");
    }

    [Fact]
    public void DuplicateAdjustmentSubmissionsHaveSingleServerEffectIdempotently()
    {
        _engine.SetOperator(_adminUser);

        var idempotencyKey = "KEY-ADJ-001";
        var adjCmd = new SubmitInventoryAdjustmentCommand(
            StockItemId: _stockItemIdSugar,
            LocationId: _mainWarehouseId,
            DeltaQuantity: 15.00m,
            Reason: "Haftalık sayım fazlası",
            IdempotencyKey: idempotencyKey,
            ExpectedRowVersion: 1);

        // First adjustment
        var result1 = _engine.SubmitAdjustment(adjCmd);
        result1.Success.Should().BeTrue();
        _engine.AdjustmentMovementCount.Should().Be(1);

        var bal1 = _engine.GetStockBalance(_stockItemIdSugar, _mainWarehouseId);
        bal1!.OnHandBalance.Should().Be(65.00m); // 50 + 15

        // Duplicate adjustment submission with same idempotency key
        var result2 = _engine.SubmitAdjustment(adjCmd);
        result2.Success.Should().BeTrue();

        // ACCEPTANCE CRITERION 2: single server effect
        _engine.AdjustmentMovementCount.Should().Be(1);
        var bal2 = _engine.GetStockBalance(_stockItemIdSugar, _mainWarehouseId);
        bal2!.OnHandBalance.Should().Be(65.00m, "Duplicate adjustment must not alter stock twice");
    }

    [Fact]
    public void StaleRowVersionAdjustmentIsRejected()
    {
        _engine.SetOperator(_adminUser);

        // Balance row version is 1, client sends stale row version 99
        var staleAdjCmd = new SubmitInventoryAdjustmentCommand(
            StockItemId: _stockItemIdSugar,
            LocationId: _mainWarehouseId,
            DeltaQuantity: -5.00m,
            Reason: "Sayım eksiği",
            IdempotencyKey: "KEY-STALE-ADJ",
            ExpectedRowVersion: 99);

        var result = _engine.SubmitAdjustment(staleAdjCmd);

        // ACCEPTANCE CRITERION 3: "eski satır reddedildi"
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("CONCURRENCY_CONFLICT");
        result.ErrorMessage.Should().Contain("Kayıt başka bir kullanıcı veya terminal tarafından güncellendi");
    }

    [Fact]
    public void AdjustmentWithoutMandatoryAuditReasonFails()
    {
        _engine.SetOperator(_adminUser);

        var noReasonCmd = new SubmitInventoryAdjustmentCommand(
            StockItemId: _stockItemIdSugar,
            LocationId: _mainWarehouseId,
            DeltaQuantity: 5.00m,
            Reason: "   ", // Empty audit reason
            IdempotencyKey: "KEY-NO-REASON",
            ExpectedRowVersion: 1);

        var result = _engine.SubmitAdjustment(noReasonCmd);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("AUDIT_REASON_REQUIRED");
        result.ErrorMessage.Should().Be("Stok ayarlaması için denetim gerekçesi girilmesi zorunludur.");
    }

    [Fact]
    public void WasteRecordingValidatesAvailableStockAndAuditReason()
    {
        _engine.SetOperator(_adminUser);

        // Attempt 1: Empty reason -> rejected
        var noReasonCmd = new RecordWasteCommand(
            StockItemId: _stockItemIdFlour,
            LocationId: _mainWarehouseId,
            Quantity: 5.00m,
            WasteReason: "",
            IdempotencyKey: "KEY-WASTE-1",
            ExpectedRowVersion: 1);

        var result1 = _engine.RecordWaste(noReasonCmd);
        result1.Success.Should().BeFalse();
        result1.ErrorMessage.Should().Be("Atık kaydı için denetim gerekçesi (nedeni) girilmesi zorunludur.");

        // Attempt 2: Exceeding available balance (available is 80kg, requesting 100kg waste) -> rejected
        var excessWasteCmd = new RecordWasteCommand(
            StockItemId: _stockItemIdFlour,
            LocationId: _mainWarehouseId,
            Quantity: 100.00m,
            WasteReason: "Nemlenme nedeniyle çuval bozuldu",
            IdempotencyKey: "KEY-WASTE-2",
            ExpectedRowVersion: 1);

        var result2 = _engine.RecordWaste(excessWasteCmd);
        result2.Success.Should().BeFalse();
        result2.ErrorCode.Should().Be("INSUFFICIENT_AVAILABLE_STOCK");

        // Attempt 3: Valid waste recording -> succeeds and reduces balance
        var validWasteCmd = new RecordWasteCommand(
            StockItemId: _stockItemIdFlour,
            LocationId: _mainWarehouseId,
            Quantity: 10.00m,
            WasteReason: "Çuval yırtılması ve dökülme",
            IdempotencyKey: "KEY-WASTE-3",
            ExpectedRowVersion: 1);

        var result3 = _engine.RecordWaste(validWasteCmd);
        result3.Success.Should().BeTrue();

        var balanceAfter = _engine.GetStockBalance(_stockItemIdFlour, _mainWarehouseId);
        balanceAfter!.OnHandBalance.Should().Be(90.00m); // 100 - 10
        balanceAfter.AvailableBalance.Should().Be(70.00m); // 80 - 10
    }

    [Fact]
    public void UnauthorizedOperatorCannotPerformInventoryOrPurchasingActions()
    {
        _engine.SetOperator(_unauthorizedUser);

        // Unauthorized goods receipt
        var receiveResult = _engine.ReceiveGoods(new ReceiveGoodsCommand(
            PurchaseOrderId: _purchaseOrderId,
            ReceiptNumber: "IRS-UNAUTH",
            LocationId: _mainWarehouseId,
            Lines: new List<ReceiveGoodsLineInput> { new(_stockItemIdFlour, 10m, 25m) },
            IdempotencyKey: "KEY-UNAUTH-1",
            ExpectedOrderRowVersion: 1));

        receiveResult.Success.Should().BeFalse();
        receiveResult.ErrorCode.Should().Be("PERMISSION_DENIED");
        receiveResult.ErrorMessage.Should().Be("Satın alma kabul işlemleri için yetkiniz bulunmamaktadır.");

        // Unauthorized adjustment
        var adjResult = _engine.SubmitAdjustment(new SubmitInventoryAdjustmentCommand(
            StockItemId: _stockItemIdFlour,
            LocationId: _mainWarehouseId,
            DeltaQuantity: 5m,
            Reason: "Sayım",
            IdempotencyKey: "KEY-UNAUTH-2",
            ExpectedRowVersion: 1));

        adjResult.Success.Should().BeFalse();
        adjResult.ErrorCode.Should().Be("PERMISSION_DENIED");
        adjResult.ErrorMessage.Should().Be("Stok ayarlama ve atık kaydı için yetkiniz bulunmamaktadır.");

        // Unauthorized waste
        var wasteResult = _engine.RecordWaste(new RecordWasteCommand(
            StockItemId: _stockItemIdFlour,
            LocationId: _mainWarehouseId,
            Quantity: 2m,
            WasteReason: "Bozulma",
            IdempotencyKey: "KEY-UNAUTH-3",
            ExpectedRowVersion: 1));

        wasteResult.Success.Should().BeFalse();
        wasteResult.ErrorCode.Should().Be("PERMISSION_DENIED");
        wasteResult.ErrorMessage.Should().Be("Stok ayarlama ve atık kaydı için yetkiniz bulunmamaktadır.");
    }

    [Fact]
    public void InventoryPurchasingUiSatisfiesV0Cmp005AccessibilityCriteria()
    {
        var elements = new[]
        {
            "btn-receive-goods",
            "btn-submit-adjustment",
            "btn-record-waste",
            "card-stock-balance"
        };

        foreach (var el in elements)
        {
            var meta = InventoryPurchasingEngine.GetAccessibilityMetadata(el);
            meta.MinTargetSizePx.Should().BeGreaterOrEqualTo(44, "WCAG 2.2 AA target size must be at least 44px");
            meta.AriaLabel.Should().NotBeNullOrWhiteSpace("Interactive operations UI controls must have accessible Turkish labels");
            meta.HighContrastCompliance.Should().BeTrue("Operations UI must satisfy high contrast requirements");
        }
    }
}
