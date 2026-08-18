namespace ALKAROS.Clients.WaiterPwa.OrderEntry;

/// <summary>
/// Domain controller for Waiter PWA Table Order Entry (V1-WTR-002, PDF:I.7-I.10, V0-CMP-005).
/// Enforces idempotent submission, stale table conflict protection, and draft preservation.
/// </summary>
public sealed class WaiterOrderEntryEngine
{
    private WaiterTableOption? _selectedTable;
    private readonly List<WaiterDraftItem> _items = new();
    private string? _orderNote;
    private string _currentIdempotencyKey = Guid.NewGuid().ToString("N");
    private bool _isSubmitting;
    private string? _errorMessage;

    public WaiterTableOption? SelectedTable => _selectedTable;
    public string CurrentIdempotencyKey => _currentIdempotencyKey;
    public bool IsSubmitting => _isSubmitting;
    public string? ErrorMessage => _errorMessage;

    public WaiterOrderDraft? CurrentDraft => _selectedTable is not null
        ? new WaiterOrderDraft(_selectedTable.TableId, _selectedTable.TableNumber, _selectedTable.ExpectedRowVersion, _items.ToList(), _orderNote)
        : null;

    public void SelectTable(WaiterTableOption table)
    {
        ArgumentNullException.ThrowIfNull(table);
        _selectedTable = table;
        _errorMessage = null;
    }

    public void AddItem(
        Guid productId,
        string productName,
        decimal unitPrice,
        int quantity = 1,
        IEnumerable<string>? modifiers = null,
        string? specialInstructions = null)
    {
        if (string.IsNullOrWhiteSpace(productName))
            throw new ArgumentException("Product name cannot be empty.", nameof(productName));

        if (quantity <= 0) quantity = 1;

        var draftItem = new WaiterDraftItem(
            Guid.NewGuid(),
            productId,
            productName,
            quantity,
            unitPrice,
            modifiers?.ToList() ?? new List<string>(),
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
            _items[index] = _items[index] with { Quantity = newQuantity };
        }
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
        _errorMessage = null;
    }

    /// <summary>
    /// Initiates submission. Idempotency key is preserved during active transmission to prevent duplicates (V1-WTR-002 Acceptance Evidence #1).
    /// </summary>
    public string BeginSubmission()
    {
        if (_isSubmitting)
        {
            return _currentIdempotencyKey;
        }

        _isSubmitting = true;
        _errorMessage = null;
        return _currentIdempotencyKey;
    }

    public void HandleSubmissionSuccess(Guid orderId)
    {
        _isSubmitting = false;
        _errorMessage = null;
        ClearDraft();
    }

    /// <summary>
    /// Handles table concurrency conflicts. Does not silently relocate the order (V1-WTR-002 Acceptance Evidence #2).
    /// </summary>
    public void HandleTableConflict(int serverTableVersion)
    {
        _isSubmitting = false;
        _errorMessage = $"Masa {_selectedTable?.TableNumber} durumu değişti (Sunucu Versiyonu: {serverTableVersion}). Sipariş sessizce taşınmadı; lütfen masayı tekrar kontrol ediniz.";
        // Draft is preserved
    }

    public void HandleGenericFailure(string errorMessage)
    {
        _isSubmitting = false;
        _errorMessage = errorMessage;
    }
}
