namespace ALKAROS.Clients.Cashier.OperationsStatus;

/// <summary>
/// Domain controller for cashier operational status view and authorized print recovery (V1-CUI-003, PDF:I.16-I.19, V1-KIT-004, V0-CMP-005).
/// </summary>
public sealed class OperationsStatusEngine
{
    private readonly List<OrderBillStatusView> _activeOrders = new();
    private string? _errorMessage;

    public IReadOnlyList<OrderBillStatusView> ActiveOrders => _activeOrders.AsReadOnly();
    public string? ErrorMessage => _errorMessage;

    public void LoadOperations(IEnumerable<OrderBillStatusView> orders)
    {
        _activeOrders.Clear();
        if (orders is not null)
        {
            _activeOrders.AddRange(orders);
        }
        _errorMessage = null;
    }

    public OrderBillStatusView? GetOrderByTable(string tableNumber)
    {
        return _activeOrders.FirstOrDefault(o => string.Equals(o.TableNumber, tableNumber, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Validates and triggers a reprint command adhering to V1-KIT-004 rules.
    /// Requires permission and mandatory reason (Acceptance Evidence #1 & #2).
    /// </summary>
    public bool ValidateAndExecuteReprint(
        RequestReprintCommand command,
        out string? validationError)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.OperatorId == Guid.Empty)
        {
            validationError = "Geçersiz operatör kimliği.";
            _errorMessage = validationError;
            return false;
        }

        if (!command.HasReprintPermission)
        {
            validationError = "Yeniden yazdırma yetkiniz bulunmamaktadır. Süpervizör onayı gereklidir.";
            _errorMessage = validationError;
            return false;
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            validationError = "Yeniden yazdırma için denetim gerekçesi (nedeni) girilmesi zorunludur.";
            _errorMessage = validationError;
            return false;
        }

        validationError = null;
        _errorMessage = null;
        return true;
    }

    public void ClearError()
    {
        _errorMessage = null;
    }
}
