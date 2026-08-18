namespace ALKAROS.Clients.WaiterPwa.OrderEntry;

/// <summary>
/// Table option for waiter order entry (V1-WTR-002, PDF:I.7-I.10).
/// </summary>
public sealed record WaiterTableOption(
    Guid TableId,
    string TableNumber,
    string Status,
    int ExpectedRowVersion);

/// <summary>
/// A line item in the waiter mobile draft (V1-WTR-002).
/// </summary>
public sealed record WaiterDraftItem(
    Guid ItemId,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    IReadOnlyList<string> Modifiers,
    string? SpecialInstructions)
{
    public decimal LineTotal => UnitPrice * Quantity;
}

/// <summary>
/// Waiter mobile working order draft (V1-WTR-002).
/// </summary>
public sealed record WaiterOrderDraft(
    Guid TableId,
    string TableNumber,
    int ExpectedTableVersion,
    IReadOnlyList<WaiterDraftItem> Items,
    string? OrderNote)
{
    public decimal TotalAmount => Items.Sum(i => i.LineTotal);
    public int TotalItemCount => Items.Sum(i => i.Quantity);
}

/// <summary>
/// Waiter order submission payload (V1-WTR-002).
/// </summary>
public sealed record WaiterOrderSubmissionResult(
    bool IsSuccess,
    Guid? OrderId,
    string IdempotencyKey,
    string? ErrorMessage,
    bool IsTableConflict);
