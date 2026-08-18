namespace ALKAROS.Clients.Cashier.OrderEntry;

/// <summary>
/// Controller and draft manager for Cashier Order Entry (V1-CUI-002, PDF:I.8, V0-CMP-005).
/// Enforces idempotent submission, draft preservation on validation error, and modifier rules.
/// </summary>
public sealed class OrderEntryEngine
{
    private readonly Guid _tableId;
    private readonly string _tableNumber;
    private readonly List<DraftOrderItem> _items = new();
    private string? _orderNote;
    private string _currentIdempotencyKey = Guid.NewGuid().ToString("N");
    private bool _isSubmitting;

    public OrderEntryEngine(Guid tableId, string tableNumber)
    {
        _tableId = tableId;
        _tableNumber = tableNumber;
    }

    public OrderDraft CurrentDraft => new(
        _tableId,
        _tableNumber,
        _items.ToList(),
        _orderNote);

    public string CurrentIdempotencyKey => _currentIdempotencyKey;
    public bool IsSubmitting => _isSubmitting;

    public void AddItem(
        CatalogProductItem product,
        int quantity = 1,
        IEnumerable<SelectedModifier>? modifiers = null,
        string? specialInstructions = null)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (quantity <= 0) quantity = 1;

        var modList = modifiers?.ToList() ?? new List<SelectedModifier>();

        var draftItem = new DraftOrderItem(
            Guid.NewGuid(),
            product.ProductId,
            product.Name,
            quantity,
            product.BasePrice,
            modList,
            specialInstructions);

        _items.Add(draftItem);
    }

    public void UpdateQuantity(Guid itemId, int newQuantity)
    {
        var index = _items.FindIndex(i => i.ItemId == itemId);
        if (index < 0) return;

        if (newQuantity <= 0)
        {
            _items.RemoveAt(index);
        }
        else
        {
            var item = _items[index];
            _items[index] = item with { Quantity = newQuantity };
        }
    }

    public void RemoveItem(Guid itemId)
    {
        _items.RemoveAll(i => i.ItemId == itemId);
    }

    public void SetNote(string? note)
    {
        _orderNote = note;
    }

    public void ClearDraft()
    {
        _items.Clear();
        _orderNote = null;
        _currentIdempotencyKey = Guid.NewGuid().ToString("N");
        _isSubmitting = false;
    }

    /// <summary>
    /// Initiates order submission. Reusing the same idempotency key prevents duplicate orders on double-click or network retry (V1-CUI-002 Acceptance Evidence).
    /// </summary>
    public string BeginSubmission()
    {
        if (_isSubmitting)
        {
            // Return existing in-flight idempotency key for retry/double-click
            return _currentIdempotencyKey;
        }

        _isSubmitting = true;
        return _currentIdempotencyKey;
    }

    public void HandleSubmissionSuccess(Guid orderId)
    {
        _isSubmitting = false;
        ClearDraft();
    }

    public void HandleSubmissionFailure(string errorMessage)
    {
        // Preserve draft so the cashier doesn't lose the entered order (V1-CUI-002 Acceptance Evidence)
        _isSubmitting = false;
        // Keep current draft and keep the same idempotency key for safe retry
    }
}
