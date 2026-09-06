namespace ALKAROS.Clients.Cashier.Production;

/// <summary>
/// Domain controller and state engine for Production Batch execution UI (V11-UI-002, PDF:I.22-I.23, V0-CMP-005).
/// Enforces idempotent batch completion, read-only immutable recipe versions,
/// stock shortfall breakdown, recoverable error handling, and accessibility compliance.
/// </summary>
public sealed class ProductionBatchEngine
{
    private ProductionOperator? _currentOperator;
    private readonly Dictionary<Guid, ProductionBatchView> _batches = new();
    private readonly Dictionary<Guid, RecipeVersionReadOnlyView> _recipeVersions = new();
    private readonly Dictionary<Guid, decimal> _stockBalances = new();
    private readonly Dictionary<Guid, StockItemInfo> _stockItemInfos = new();
    private int _outputGenerationCount;
    private string? _lastErrorMessage;

    public ProductionOperator? CurrentOperator => _currentOperator;
    public string? LastErrorMessage => _lastErrorMessage;
    public int OutputGenerationCount => _outputGenerationCount;

    public void SetOperator(ProductionOperator op)
    {
        _currentOperator = op;
        _lastErrorMessage = null;
    }

    public void ClearError()
    {
        _lastErrorMessage = null;
    }

    public void LoadStockBalances(IDictionary<Guid, decimal> balances, IEnumerable<StockItemInfo>? items = null)
    {
        _stockBalances.Clear();
        if (balances != null)
        {
            foreach (var kvp in balances)
                _stockBalances[kvp.Key] = kvp.Value;
        }

        if (items != null)
        {
            _stockItemInfos.Clear();
            foreach (var item in items)
                _stockItemInfos[item.StockItemId] = item;
        }
    }

    public void LoadRecipeVersions(IEnumerable<RecipeVersionReadOnlyView> versions)
    {
        _recipeVersions.Clear();
        if (versions != null)
        {
            foreach (var v in versions)
                _recipeVersions[v.VersionId] = v;
        }
    }

    public void LoadBatches(IEnumerable<ProductionBatchView> batches)
    {
        _batches.Clear();
        if (batches != null)
        {
            foreach (var b in batches)
                _batches[b.Id] = b;
        }
    }

    public ProductionBatchView? GetBatch(Guid batchId) =>
        _batches.TryGetValue(batchId, out var b) ? b : null;

    public IReadOnlyList<ProductionBatchView> GetAllBatches() =>
        _batches.Values.OrderByDescending(b => b.CreatedAt).ToList().AsReadOnly();

    public decimal GetStockBalance(Guid stockItemId) =>
        _stockBalances.TryGetValue(stockItemId, out var bal) ? bal : 0m;

    #region Production Batch Lifecycle

    public ProductionOperationResult<ProductionBatchView> PlanBatch(PlanProductionBatchCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (_currentOperator == null || !_currentOperator.HasPermission(ProductionPermissions.Execute))
        {
            _lastErrorMessage = "Üretim işlemleri için yetkiniz bulunmamaktadır.";
            return ProductionOperationResult.Fail<ProductionBatchView>(_lastErrorMessage, "PERMISSION_DENIED");
        }

        if (cmd.PlannedQuantity <= 0)
        {
            _lastErrorMessage = "Planlanan miktar sıfırdan büyük olmalıdır.";
            return ProductionOperationResult.Fail<ProductionBatchView>(_lastErrorMessage, "PLANNED_QUANTITY_POSITIVE");
        }

        if (!_recipeVersions.TryGetValue(cmd.RecipeVersionId, out var recipeVersion))
        {
            _lastErrorMessage = "Reçete versiyonu bulunamadı.";
            return ProductionOperationResult.Fail<ProductionBatchView>(_lastErrorMessage, "RECIPE_VERSION_NOT_FOUND");
        }

        var consumptions = PreviewConsumptions(recipeVersion, cmd.PlannedQuantity);

        var batchId = Guid.NewGuid();
        var batchNumber = $"PRD-{DateTime.UtcNow:yyyyMMdd}-{_batches.Count + 1:D3}";

        var batch = new ProductionBatchView(
            Id: batchId,
            BatchNumber: batchNumber,
            RecipeId: cmd.RecipeId,
            RecipeName: recipeVersion.RecipeName,
            RecipeVersionId: cmd.RecipeVersionId,
            RecipeVersionNumber: recipeVersion.VersionNumber,
            PlannedQuantity: cmd.PlannedQuantity,
            ActualQuantity: null,
            UnitCode: "porsiyon",
            Status: "Planned",
            LocationId: cmd.LocationId,
            LocationName: cmd.LocationName,
            Consumptions: consumptions,
            Output: null,
            CreatedAt: DateTimeOffset.UtcNow,
            CompletedAt: null,
            FailureReason: null,
            MissingStock: Array.Empty<StockShortfallItem>());

        _batches[batch.Id] = batch;
        _lastErrorMessage = null;
        return ProductionOperationResult.Ok<ProductionBatchView>(batch);
    }

    public ProductionOperationResult<ProductionBatchView> StartBatch(StartProductionBatchCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (_currentOperator == null || !_currentOperator.HasPermission(ProductionPermissions.Execute))
        {
            _lastErrorMessage = "Üretim işlemleri için yetkiniz bulunmamaktadır.";
            return ProductionOperationResult.Fail<ProductionBatchView>(_lastErrorMessage, "PERMISSION_DENIED");
        }

        if (!_batches.TryGetValue(cmd.BatchId, out var batch))
        {
            _lastErrorMessage = "Üretim partisi bulunamadı.";
            return ProductionOperationResult.Fail<ProductionBatchView>(_lastErrorMessage, "BATCH_NOT_FOUND");
        }

        if (!string.Equals(batch.Status, "Planned", StringComparison.OrdinalIgnoreCase))
        {
            _lastErrorMessage = $"Yalnızca planlanmış partiler başlatılabilir. Mevcut durum: '{batch.Status}'.";
            return ProductionOperationResult.Fail<ProductionBatchView>(_lastErrorMessage, "INVALID_STATE");
        }

        // Check stock availability
        var shortfalls = CheckStockShortfalls(batch.Consumptions);
        if (shortfalls.Count > 0)
        {
            // Acceptance Evidence #1: Failure keeps batch recoverable and details missing stock!
            _lastErrorMessage = "Üretim başlatılamadı: Yetersiz stok mevcuttur. Lütfen eksik stokları tamamlayınız.";
            var recoverableBatch = batch with
            {
                MissingStock = shortfalls,
                FailureReason = _lastErrorMessage
            };
            _batches[recoverableBatch.Id] = recoverableBatch;

            return ProductionOperationResult.Fail<ProductionBatchView>(_lastErrorMessage, "INSUFFICIENT_STOCK");
        }

        var startedBatch = batch with
        {
            Status = "InProgress",
            FailureReason = null,
            MissingStock = Array.Empty<StockShortfallItem>()
        };

        _batches[startedBatch.Id] = startedBatch;
        _lastErrorMessage = null;
        return ProductionOperationResult.Ok<ProductionBatchView>(startedBatch);
    }

    /// <summary>
    /// Completes production batch.
    /// Acceptance Evidence #1: "Duplicate complete tek output üretir". Calling complete on an already
    /// completed batch is idempotent and returns the existing result without duplicating outputs or consumptions.
    /// </summary>
    public ProductionOperationResult<ProductionBatchView> CompleteBatch(CompleteProductionBatchCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (_currentOperator == null || !_currentOperator.HasPermission(ProductionPermissions.Execute))
        {
            _lastErrorMessage = "Üretim işlemleri için yetkiniz bulunmamaktadır.";
            return ProductionOperationResult.Fail<ProductionBatchView>(_lastErrorMessage, "PERMISSION_DENIED");
        }

        if (!_batches.TryGetValue(cmd.BatchId, out var batch))
        {
            _lastErrorMessage = "Üretim partisi bulunamadı.";
            return ProductionOperationResult.Fail<ProductionBatchView>(_lastErrorMessage, "BATCH_NOT_FOUND");
        }

        // IDEMPOTENCY INVARIANT: If already completed, do NOT create another output or deduct stock!
        if (string.Equals(batch.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            _lastErrorMessage = null;
            return ProductionOperationResult.Ok<ProductionBatchView>(batch);
        }

        if (string.Equals(batch.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            _lastErrorMessage = "İptal edilmiş üretim partisi tamamlanamaz.";
            return ProductionOperationResult.Fail<ProductionBatchView>(_lastErrorMessage, "BATCH_CANCELLED");
        }

        if (cmd.ActualQuantity <= 0)
        {
            _lastErrorMessage = "Gerçekleşen üretim miktarı sıfırdan büyük olmalıdır.";
            return ProductionOperationResult.Fail<ProductionBatchView>(_lastErrorMessage, "ACTUAL_QUANTITY_POSITIVE");
        }

        // Deduct consumptions from available stock
        foreach (var consumption in batch.Consumptions)
        {
            if (_stockBalances.TryGetValue(consumption.StockItemId, out var current))
            {
                _stockBalances[consumption.StockItemId] = Math.Max(0m, current - consumption.RequiredQuantity);
            }
        }

        // Increment authoritative output counter exactly once
        _outputGenerationCount++;

        var output = new ProductionOutputSummary(
            OutputStockItemId: Guid.NewGuid(),
            ItemName: batch.RecipeName,
            Quantity: cmd.ActualQuantity,
            UnitCode: batch.UnitCode,
            ProducedAt: DateTimeOffset.UtcNow);

        var completedBatch = batch with
        {
            Status = "Completed",
            ActualQuantity = cmd.ActualQuantity,
            Output = output,
            CompletedAt = DateTimeOffset.UtcNow,
            FailureReason = null
        };

        _batches[completedBatch.Id] = completedBatch;
        _lastErrorMessage = null;
        return ProductionOperationResult.Ok<ProductionBatchView>(completedBatch);
    }

    public ProductionOperationResult<ProductionBatchView> CancelBatch(CancelProductionBatchCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (_currentOperator == null || !_currentOperator.HasPermission(ProductionPermissions.Execute))
        {
            _lastErrorMessage = "Üretim işlemleri için yetkiniz bulunmamaktadır.";
            return ProductionOperationResult.Fail<ProductionBatchView>(_lastErrorMessage, "PERMISSION_DENIED");
        }

        if (!_batches.TryGetValue(cmd.BatchId, out var batch))
        {
            _lastErrorMessage = "Üretim partisi bulunamadı.";
            return ProductionOperationResult.Fail<ProductionBatchView>(_lastErrorMessage, "BATCH_NOT_FOUND");
        }

        if (string.Equals(batch.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            _lastErrorMessage = "Tamamlanmış üretim partisi iptal edilemez.";
            return ProductionOperationResult.Fail<ProductionBatchView>(_lastErrorMessage, "CANNOT_CANCEL_COMPLETED");
        }

        if (string.IsNullOrWhiteSpace(cmd.Reason))
        {
            _lastErrorMessage = "İptal gerekçesi girilmesi zorunludur.";
            return ProductionOperationResult.Fail<ProductionBatchView>(_lastErrorMessage, "REASON_REQUIRED");
        }

        var cancelledBatch = batch with
        {
            Status = "Cancelled",
            FailureReason = cmd.Reason.Trim()
        };

        _batches[cancelledBatch.Id] = cancelledBatch;
        _lastErrorMessage = null;
        return ProductionOperationResult.Ok<ProductionBatchView>(cancelledBatch);
    }

    #endregion

    #region Recipe Immutability Guard

    /// <summary>
    /// Guard preventing any mutation of recipe versions referenced in production batches.
    /// Acceptance Evidence #1: "referenced RecipeVersion read-only kalır".
    /// </summary>
    public ProductionOperationResult AttemptEditReferencedRecipeVersion(AttemptEditReferencedRecipeVersionCommand cmd)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        // Referenced recipe versions are strictly immutable and read-only
        _lastErrorMessage = "Üretim partisinde referans verilen tarif versiyonları salt okunurdur (değiştirilemez).";
        return ProductionOperationResult.Fail(_lastErrorMessage, "REFERENCED_RECIPE_VERSION_IMMUTABLE");
    }

    #endregion

    #region Private Helpers

    private List<ProductionStockEffectPreview> PreviewConsumptions(RecipeVersionReadOnlyView recipeVersion, decimal plannedQuantity)
    {
        var result = new List<ProductionStockEffectPreview>();
        foreach (var ingredient in recipeVersion.Ingredients)
        {
            // Effective quantity with loss percentage
            decimal effectivePerUnit = ingredient.Quantity * (1m + (ingredient.LossPercentage / 100m));
            decimal totalRequired = effectivePerUnit * plannedQuantity;

            decimal available = GetStockBalance(ingredient.StockItemId);

            result.Add(new ProductionStockEffectPreview(
                StockItemId: ingredient.StockItemId,
                ItemCode: _stockItemInfos.TryGetValue(ingredient.StockItemId, out var info) ? info.ItemCode : "STK-UNKNOWN",
                ItemName: ingredient.ItemName,
                RequiredQuantity: totalRequired,
                UnitCode: ingredient.UnitCode,
                AvailableStock: available,
                HasSufficientStock: available >= totalRequired));
        }

        return result;
    }

    private static List<StockShortfallItem> CheckStockShortfalls(IReadOnlyList<ProductionStockEffectPreview> consumptions)
    {
        var shortfalls = new List<StockShortfallItem>();
        foreach (var c in consumptions)
        {
            if (c.AvailableStock < c.RequiredQuantity)
            {
                shortfalls.Add(new StockShortfallItem(
                    StockItemId: c.StockItemId,
                    ItemCode: c.ItemCode,
                    ItemName: c.ItemName,
                    RequiredQuantity: c.RequiredQuantity,
                    AvailableQuantity: c.AvailableStock,
                    ShortfallQuantity: c.RequiredQuantity - c.AvailableStock,
                    UnitCode: c.UnitCode));
            }
        }
        return shortfalls;
    }

    #endregion

    #region Operations UI Accessibility Compliance (V0-CMP-005, WCAG 2.2 AA)

    public static ProductionUiAccessibilityMetadata GetAccessibilityMetadata(string elementId)
    {
        return elementId switch
        {
            "btn-plan-batch" => new ProductionUiAccessibilityMetadata(
                elementId,
                AriaLabel: "Yeni üretim partisi planla",
                Role: "button",
                IsKeyboardFocusable: true,
                MinTargetSizePx: 48,
                HelpText: "Seçili reçete ve lokasyon için yeni üretim partisi oluşturur.",
                HighContrastCompliance: true),

            "btn-start-batch" => new ProductionUiAccessibilityMetadata(
                elementId,
                AriaLabel: "Üretimi başlat",
                Role: "button",
                IsKeyboardFocusable: true,
                MinTargetSizePx: 44,
                HelpText: "Stok uygunluğunu denetleyerek partiyi üretim aşamasına alır.",
                HighContrastCompliance: true),

            "btn-complete-batch" => new ProductionUiAccessibilityMetadata(
                elementId,
                AriaLabel: "Üretimi tamamla ve ürün girişini kaydet",
                Role: "button",
                IsKeyboardFocusable: true,
                MinTargetSizePx: 48,
                HelpText: "Fiili miktara göre stok tüketimlerini düşer ve mamul stoğunu oluşturur.",
                HighContrastCompliance: true),

            "btn-cancel-batch" => new ProductionUiAccessibilityMetadata(
                elementId,
                AriaLabel: "Üretim partisini iptal et",
                Role: "button",
                IsKeyboardFocusable: true,
                MinTargetSizePx: 44,
                HelpText: "Gerekçe girerek partiyi iptal eder ve stok rezervasyonunu serbest bırakır.",
                HighContrastCompliance: true),

            "card-recipe-readonly" => new ProductionUiAccessibilityMetadata(
                elementId,
                AriaLabel: "Referans verilen değişmez tarif bilgisi",
                Role: "region",
                IsKeyboardFocusable: false,
                MinTargetSizePx: 44,
                HelpText: "Üretimde kullanılan reçete sürümünü salt okunur olarak görüntüler.",
                HighContrastCompliance: true),

            "card-stock-shortfall" => new ProductionUiAccessibilityMetadata(
                elementId,
                AriaLabel: "Eksik stok detay paneli",
                Role: "alert",
                IsKeyboardFocusable: true,
                MinTargetSizePx: 44,
                HelpText: "Yetersiz kalan malzemeleri ve eksik miktarları listeler.",
                HighContrastCompliance: true),

            _ => new ProductionUiAccessibilityMetadata(
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
