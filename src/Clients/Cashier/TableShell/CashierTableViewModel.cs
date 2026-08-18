namespace ALKAROS.Clients.Cashier.TableShell;

/// <summary>
/// Status category for table display in cashier shell (V1-CUI-001, PDF:I.9-I.10).
/// </summary>
public enum TableViewStatus
{
    Available,
    Occupied,
    Reserved,
    Maintenance
}

/// <summary>
/// Operational action badge for 2-dimensional table state (V1-CUI-001, DESIGN.md).
/// </summary>
public enum TableOperationalBadge
{
    None,
    BillRequested,
    KitchenCooking,
    KitchenReady
}

/// <summary>
/// Client view model for a table card in the cashier matrix (V1-CUI-001, DESIGN.md).
/// </summary>
public sealed record TableCardViewModel(
    Guid TableId,
    string TableNumber,
    string Section,
    TableViewStatus Status,
    int Capacity,
    decimal? ActiveBillAmount,
    int RowVersion,
    DateTimeOffset? OccupiedSince,
    bool IsSelected = false,
    TableOperationalBadge OperationalBadge = TableOperationalBadge.None);

/// <summary>
/// State payload for the Cashier Table Shell (V1-CUI-001).
/// </summary>
public sealed record CashierShellState(
    CashierSession? Session,
    string ActiveSection,
    IReadOnlyList<TableCardViewModel> Tables,
    TableCardViewModel? SelectedTable,
    string? ErrorMessage,
    bool IsSessionExpired);
