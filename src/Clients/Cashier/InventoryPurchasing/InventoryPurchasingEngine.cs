namespace ALKAROS.Clients.Cashier.InventoryPurchasing;

/// <summary>
/// Domain controller and state engine for Inventory & Purchasing Operations UI (V11-UI-003, PDF:I.23-I.25, V0-CMP-005).
/// Enforces immutable projected stock balances, idempotent receipts/adjustments,
/// optimistic concurrency control, mandatory audit reasons, and accessibility criteria.
/// </summary>
public sealed class InventoryPurchasingEngine
{
    private InventoryPurchasingOperator? _currentOperator;
    private readonly Dictionary<(Guid StockItemId, Guid LocationId), StockBalanceView> _balances = new();
    private readonly Dictionary<Guid, PurchaseOrderView> _purchaseOrders = new();
    private readonly Dictionary<string, GoodsReceiptView> _processedReceipts = new();
    private readonly Dictionary<string, InventoryAdjustmentView> _processedAdjustments = new();
    private readonly Dictionary<string, WasteRecordView> _processedWaste = new();

    private int _goodsReceiptMovementCount;
    private int _adjustmentMovementCount;
    private string? _lastErrorMessage;

    public InventoryPurchasingOperator? CurrentOperator => _currentOperator;
    public string? LastErrorMessage => _lastErrorMessage;
    public int GoodsReceiptMovementCount => _goodsReceiptMovementCount;
    public int AdjustmentMovementCount => _adjustmentMovementCount;

    public void SetOperator(InventoryPurchasingOperator op)
    {
        _currentOperator = op;
        _lastErrorMessage = null;
    }

    public void ClearError()
    {
        _lastErrorMessage = null;
    }

    #region Stock Balance Queries (Projection - READ ONLY)

    public void LoadStockBalances(IEnumerable<StockBalanceView> balances)
    {
        _balances.Clear();
        if (balances != null)
        {
            foreach (var b in balances)
                _balances[(b.StockItemId, b.LocationId)] = b;
        }
    }

    public StockBalanceView? GetStockBalance(Guid stockItemId, Guid locationId) =>
        _balances.TryGetValue((stockItemId, locationId), out var b) ? b : null;

    public IReadOnlyList<StockBalanceView> GetBalancesByLocation(Guid locationId) =>
        _balances.Values.Where(b => b.LocationId == locationId).OrderBy(b => b.ItemCode).ToList().AsReadOnly();

    public IReadOnlyList<StockBalanceView> GetAllBalances() =>
        _balances.Values.OrderBy(b => b.ItemCode).ToList().AsReadOnly();

    /// <summary>
    /// Prevents direct mutation of stock balance.
    /// Acceptance Evidence #1: "UI asla bakiyeyi doğrudan yazmaz".
    /// </summary>
    public InvPurchOperationResult AttemptDirectBalanceMutation(AttemptDirectBalanceMutationCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        _lastErrorMessage = "Stok bakiyeleri doğrudan düzenlenemez; yalnızca denetimli hareketler (alım, ayarlama, atık) ile güncellenebilir.";
        return InvPurchOperationResult.Fail(_lastErrorMessage, "DIRECT_BALANCE_MUTATION_PROHIBITED");
    }

    #endregion

    #region Purchasing & Goods Receipt

    public void LoadPurchaseOrders(IEnumerable<PurchaseOrderView> orders)
    {
        _purchaseOrders.Clear();
        if (orders != null)
        {
            foreach (var o in orders)
                _purchaseOrders[o.OrderId] = o;
        }
    }

    public PurchaseOrderView? GetPurchaseOrder(Guid orderId) =>
        _purchaseOrders.TryGetValue(orderId, out var o) ? o : null;

    /// <summary>
    /// Processes goods receipt.
    /// Acceptance Evidence #1: "tekrarlanan alım/ayarlama eyleminin tek bir sunucu etkisi vardır; eski satır reddedildi".
    /// </summary>
    public InvPurchOperationResult<GoodsReceiptView> ReceiveGoods(ReceiveGoodsCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (_currentOperator == null || !_currentOperator.HasPermission(InvPurchPermissions.PurchasingReceive))
        {
            _lastErrorMessage = "Satın alma kabul işlemleri için yetkiniz bulunmamaktadır.";
            return InvPurchOperationResult.Fail<GoodsReceiptView>(_lastErrorMessage, "PERMISSION_DENIED");
        }

        // 1. IDEMPOTENCY: Repeated receipt returns cached result with single server effect!
        if (_processedReceipts.TryGetValue(cmd.IdempotencyKey, out var existingReceipt))
        {
            _lastErrorMessage = null;
            return InvPurchOperationResult.Ok<GoodsReceiptView>(existingReceipt);
        }

        if (!_purchaseOrders.TryGetValue(cmd.PurchaseOrderId, out var order))
        {
            _lastErrorMessage = "Satın alma siparişi bulunamadı.";
            return InvPurchOperationResult.Fail<GoodsReceiptView>(_lastErrorMessage, "ORDER_NOT_FOUND");
        }

        // 2. OPTIMISTIC CONCURRENCY: Stale row version rejected! ("eski satır reddedildi")
        if (order.RowVersion != cmd.ExpectedOrderRowVersion)
        {
            _lastErrorMessage = "Kayıt başka bir kullanıcı veya terminal tarafından güncellendi. Lütfen güncel veriyi yükleyiniz.";
            return InvPurchOperationResult.Fail<GoodsReceiptView>(_lastErrorMessage, "CONCURRENCY_CONFLICT");
        }

        if (cmd.Lines == null || cmd.Lines.Count == 0)
        {
            _lastErrorMessage = "Kabul edilecek en az bir ürün kalemi girilmelidir.";
            return InvPurchOperationResult.Fail<GoodsReceiptView>(_lastErrorMessage, "LINES_EMPTY");
        }

        var receiptLines = new List<GoodsReceiptLineView>();
        foreach (var lineInput in cmd.Lines)
        {
            if (lineInput.ReceivedQuantity <= 0)
            {
                _lastErrorMessage = "Teslim alınan miktar sıfırdan büyük olmalıdır.";
                return InvPurchOperationResult.Fail<GoodsReceiptView>(_lastErrorMessage, "QUANTITY_POSITIVE");
            }

            var orderItem = order.Lines.FirstOrDefault(l => l.StockItemId == lineInput.StockItemId);
            string itemName = orderItem?.ItemName ?? "Bilinmeyen Ürün";
            string unitCode = orderItem?.UnitCode ?? "adet";

            receiptLines.Add(new GoodsReceiptLineView(
                StockItemId: lineInput.StockItemId,
                ItemName: itemName,
                Quantity: lineInput.ReceivedQuantity,
                UnitCode: unitCode,
                UnitCost: lineInput.UnitCost));

            // Increase on-hand balance and available balance
            var balanceKey = (lineInput.StockItemId, cmd.LocationId);
            if (_balances.TryGetValue(balanceKey, out var currentBal))
            {
                var newOnHand = currentBal.OnHandBalance + lineInput.ReceivedQuantity;
                var newAvailable = newOnHand - currentBal.ReservedBalance;
                _balances[balanceKey] = currentBal with
                {
                    OnHandBalance = newOnHand,
                    AvailableBalance = newAvailable,
                    RowVersion = currentBal.RowVersion + 1,
                    LastProjectedAt = DateTimeOffset.UtcNow
                };
            }
        }

        // Increment movement counter exactly once
        _goodsReceiptMovementCount++;

        // Update order row version to simulate database record bump
        _purchaseOrders[order.OrderId] = order with { RowVersion = order.RowVersion + 1 };

        var receipt = new GoodsReceiptView(
            ReceiptId: Guid.NewGuid(),
            PurchaseOrderId: cmd.PurchaseOrderId,
            ReceiptNumber: cmd.ReceiptNumber,
            LocationId: cmd.LocationId,
            Lines: receiptLines,
            ReceivedAt: DateTimeOffset.UtcNow,
            IdempotencyKey: cmd.IdempotencyKey);

        _processedReceipts[cmd.IdempotencyKey] = receipt;
        _lastErrorMessage = null;
        return InvPurchOperationResult.Ok<GoodsReceiptView>(receipt);
    }

    #endregion

    #region Inventory Adjustments & Waste

    /// <summary>
    /// Submits manual inventory adjustment.
    /// Acceptance Evidence #1: "tekrarlanan alım/ayarlama eyleminin tek bir sunucu etkisi vardır; eski satır reddedildi".
    /// </summary>
    public InvPurchOperationResult<InventoryAdjustmentView> SubmitAdjustment(SubmitInventoryAdjustmentCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (_currentOperator == null || !_currentOperator.HasPermission(InvPurchPermissions.InventoryAdjust))
        {
            _lastErrorMessage = "Stok ayarlama ve atık kaydı için yetkiniz bulunmamaktadır.";
            return InvPurchOperationResult.Fail<InventoryAdjustmentView>(_lastErrorMessage, "PERMISSION_DENIED");
        }

        // 1. IDEMPOTENCY: Repeated adjustment returns cached result with single server effect!
        if (_processedAdjustments.TryGetValue(cmd.IdempotencyKey, out var existingAdj))
        {
            _lastErrorMessage = null;
            return InvPurchOperationResult.Ok<InventoryAdjustmentView>(existingAdj);
        }

        if (string.IsNullOrWhiteSpace(cmd.Reason))
        {
            _lastErrorMessage = "Stok ayarlaması için denetim gerekçesi girilmesi zorunludur.";
            return InvPurchOperationResult.Fail<InventoryAdjustmentView>(_lastErrorMessage, "AUDIT_REASON_REQUIRED");
        }

        var balanceKey = (cmd.StockItemId, cmd.LocationId);
        if (!_balances.TryGetValue(balanceKey, out var balance))
        {
            _lastErrorMessage = "Stok kartı veya lokasyon bulunamadı.";
            return InvPurchOperationResult.Fail<InventoryAdjustmentView>(_lastErrorMessage, "BALANCE_NOT_FOUND");
        }

        // 2. OPTIMISTIC CONCURRENCY: Stale row version rejected! ("eski satır reddedildi")
        if (balance.RowVersion != cmd.ExpectedRowVersion)
        {
            _lastErrorMessage = "Kayıt başka bir kullanıcı veya terminal tarafından güncellendi. Lütfen güncel veriyi yükleyiniz.";
            return InvPurchOperationResult.Fail<InventoryAdjustmentView>(_lastErrorMessage, "CONCURRENCY_CONFLICT");
        }

        var newOnHand = balance.OnHandBalance + cmd.DeltaQuantity;
        if (newOnHand < 0)
        {
            _lastErrorMessage = "Stok bakiyesi negatif olamaz.";
            return InvPurchOperationResult.Fail<InventoryAdjustmentView>(_lastErrorMessage, "BALANCE_CANNOT_BE_NEGATIVE");
        }

        var newAvailable = newOnHand - balance.ReservedBalance;
        var updatedBalance = balance with
        {
            OnHandBalance = newOnHand,
            AvailableBalance = newAvailable,
            RowVersion = balance.RowVersion + 1,
            LastProjectedAt = DateTimeOffset.UtcNow
        };
        _balances[balanceKey] = updatedBalance;

        // Increment adjustment movement counter exactly once
        _adjustmentMovementCount++;

        var adjustment = new InventoryAdjustmentView(
            AdjustmentId: Guid.NewGuid(),
            StockItemId: cmd.StockItemId,
            ItemName: balance.ItemName,
            LocationId: cmd.LocationId,
            DeltaQuantity: cmd.DeltaQuantity,
            NewOnHandBalance: newOnHand,
            Reason: cmd.Reason.Trim(),
            IdempotencyKey: cmd.IdempotencyKey,
            AdjustedAt: DateTimeOffset.UtcNow);

        _processedAdjustments[cmd.IdempotencyKey] = adjustment;
        _lastErrorMessage = null;
        return InvPurchOperationResult.Ok<InventoryAdjustmentView>(adjustment);
    }

    /// <summary>
    /// Records general inventory waste.
    /// Acceptance Evidence #1: "eski satır reddedildi", mandatory audit reason.
    /// </summary>
    public InvPurchOperationResult<WasteRecordView> RecordWaste(RecordWasteCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (_currentOperator == null || !_currentOperator.HasPermission(InvPurchPermissions.InventoryAdjust))
        {
            _lastErrorMessage = "Stok ayarlama ve atık kaydı için yetkiniz bulunmamaktadır.";
            return InvPurchOperationResult.Fail<WasteRecordView>(_lastErrorMessage, "PERMISSION_DENIED");
        }

        if (_processedWaste.TryGetValue(cmd.IdempotencyKey, out var existingWaste))
        {
            _lastErrorMessage = null;
            return InvPurchOperationResult.Ok<WasteRecordView>(existingWaste);
        }

        if (string.IsNullOrWhiteSpace(cmd.WasteReason))
        {
            _lastErrorMessage = "Atık kaydı için denetim gerekçesi (nedeni) girilmesi zorunludur.";
            return InvPurchOperationResult.Fail<WasteRecordView>(_lastErrorMessage, "AUDIT_REASON_REQUIRED");
        }

        if (cmd.Quantity <= 0)
        {
            _lastErrorMessage = "Atık miktarı sıfırdan büyük olmalıdır.";
            return InvPurchOperationResult.Fail<WasteRecordView>(_lastErrorMessage, "QUANTITY_POSITIVE");
        }

        var balanceKey = (cmd.StockItemId, cmd.LocationId);
        if (!_balances.TryGetValue(balanceKey, out var balance))
        {
            _lastErrorMessage = "Stok kartı veya lokasyon bulunamadı.";
            return InvPurchOperationResult.Fail<WasteRecordView>(_lastErrorMessage, "BALANCE_NOT_FOUND");
        }

        // Concurrency check
        if (balance.RowVersion != cmd.ExpectedRowVersion)
        {
            _lastErrorMessage = "Kayıt başka bir kullanıcı veya terminal tarafından güncellendi. Lütfen güncel veriyi yükleyiniz.";
            return InvPurchOperationResult.Fail<WasteRecordView>(_lastErrorMessage, "CONCURRENCY_CONFLICT");
        }

        if (balance.AvailableBalance < cmd.Quantity)
        {
            _lastErrorMessage = "Yetersiz kullanılabilir stok. Mevcut kullanılabilir stoktan fazla atık kaydedilemez.";
            return InvPurchOperationResult.Fail<WasteRecordView>(_lastErrorMessage, "INSUFFICIENT_AVAILABLE_STOCK");
        }

        var newOnHand = balance.OnHandBalance - cmd.Quantity;
        var newAvailable = balance.AvailableBalance - cmd.Quantity;
        _balances[balanceKey] = balance with
        {
            OnHandBalance = newOnHand,
            AvailableBalance = newAvailable,
            RowVersion = balance.RowVersion + 1,
            LastProjectedAt = DateTimeOffset.UtcNow
        };

        var waste = new WasteRecordView(
            WasteId: Guid.NewGuid(),
            StockItemId: cmd.StockItemId,
            ItemName: balance.ItemName,
            LocationId: cmd.LocationId,
            Quantity: cmd.Quantity,
            UnitCode: balance.UnitCode,
            WasteReason: cmd.WasteReason.Trim(),
            IdempotencyKey: cmd.IdempotencyKey,
            RecordedAt: DateTimeOffset.UtcNow);

        _processedWaste[cmd.IdempotencyKey] = waste;
        _lastErrorMessage = null;
        return InvPurchOperationResult.Ok<WasteRecordView>(waste);
    }

    #endregion

    #region Operations UI Accessibility Compliance (V0-CMP-005, WCAG 2.2 AA)

    public static InvPurchUiAccessibilityMetadata GetAccessibilityMetadata(string elementId)
    {
        return elementId switch
        {
            "btn-receive-goods" => new InvPurchUiAccessibilityMetadata(
                elementId,
                AriaLabel: "Satın alma fişini onayla ve mal kabulü gerçekleştir",
                Role: "button",
                IsKeyboardFocusable: true,
                MinTargetSizePx: 48,
                HelpText: "Seçili satın alma siparişinin teslim alınan kalemlerini stoğa aktarır.",
                HighContrastCompliance: true),

            "btn-submit-adjustment" => new InvPurchUiAccessibilityMetadata(
                elementId,
                AriaLabel: "Stok sayım düzeltmesini kaydet",
                Role: "button",
                IsKeyboardFocusable: true,
                MinTargetSizePx: 48,
                HelpText: "Gerekçe girerek lokasyon bazında fiziksel sayım farkını stoğa işler.",
                HighContrastCompliance: true),

            "btn-record-waste" => new InvPurchUiAccessibilityMetadata(
                elementId,
                AriaLabel: "Stok atık veya fire kaydını tamamla",
                Role: "button",
                IsKeyboardFocusable: true,
                MinTargetSizePx: 44,
                HelpText: "Bozulma, dökülme veya hasar nedeniyle atık kaydı oluşturur.",
                HighContrastCompliance: true),

            "card-stock-balance" => new InvPurchUiAccessibilityMetadata(
                elementId,
                AriaLabel: "Salt okunur hesaplanmış stok bakiye paneli",
                Role: "region",
                IsKeyboardFocusable: false,
                MinTargetSizePx: 44,
                HelpText: "Stok hareketlerinden otomatik yansıtılan fiili, rezerve ve kullanılabilir bakiyeleri gösterir.",
                HighContrastCompliance: true),

            _ => new InvPurchUiAccessibilityMetadata(
                elementId,
                AriaLabel: elementId,
                Role: "region",
                IsKeyboardFocusable: false,
                MinTargetSizePx: 44,
                HelpText: null,
                HighContrastCompliance: true)
        };
    }

    #endregion
}
