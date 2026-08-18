namespace ALKAROS.Clients.Cashier.TableShell;

/// <summary>
/// Controller and state manager for Cashier Shell & Table View (V1-CUI-001, PDF:I.7, PDF:I.9-I.10, V0-CMP-005).
/// Enforces server-authoritative state, session timeouts, and concurrency conflict presentation.
/// </summary>
public sealed class CashierShellEngine
{
    public const string AllSections = "Tümü";

    private CashierSession? _session;
    private string _activeSection = AllSections;
    private List<TableCardViewModel> _tables = new();
    private TableCardViewModel? _selectedTable;
    private string? _errorMessage;

    public CashierShellState CurrentState => new(
        _session,
        _activeSection,
        GetFilteredTables(),
        _selectedTable,
        _errorMessage,
        IsSessionExpired());

    public void SetSession(CashierSession session)
    {
        _session = session;
        _errorMessage = null;
    }

    public void InvalidateSession()
    {
        _session = null;
        _selectedTable = null;
        _errorMessage = "Oturum süresi doldu. Lütfen tekrar giriş yapınız.";
    }

    public bool IsSessionExpired(DateTimeOffset? utcNow = null)
    {
        if (_session is null) return true;
        var now = utcNow ?? DateTimeOffset.UtcNow;
        return _session.IsExpired(now);
    }

    public void LoadTables(IEnumerable<TableCardViewModel> tables)
    {
        _tables = tables?.ToList() ?? new List<TableCardViewModel>();
        if (_selectedTable is not null)
        {
            _selectedTable = _tables.FirstOrDefault(t => t.TableId == _selectedTable.TableId);
        }
    }

    public void SetSectionFilter(string section)
    {
        _activeSection = string.IsNullOrWhiteSpace(section) ? AllSections : section.Trim();
    }

    public IReadOnlyList<TableCardViewModel> GetFilteredTables()
    {
        if (string.Equals(_activeSection, AllSections, StringComparison.OrdinalIgnoreCase))
        {
            return _tables.OrderBy(t => t.TableNumber).ToList();
        }

        return _tables
            .Where(t => string.Equals(t.Section, _activeSection, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.TableNumber)
            .ToList();
    }

    public bool SelectTable(Guid tableId)
    {
        if (IsSessionExpired())
        {
            InvalidateSession();
            return false;
        }

        var table = _tables.FirstOrDefault(t => t.TableId == tableId);
        if (table is null)
        {
            _errorMessage = "Masa bulunamadı.";
            return false;
        }

        _selectedTable = table;
        _errorMessage = null;
        return true;
    }

    public bool ApplyTableUpdate(TableCardViewModel updatedTable, int clientExpectedVersion)
    {
        if (IsSessionExpired())
        {
            InvalidateSession();
            return false;
        }

        ArgumentNullException.ThrowIfNull(updatedTable);

        // Check concurrency (Acceptance evidence: table updates reflect row-version conflicts without stale UI overrides)
        if (updatedTable.RowVersion != clientExpectedVersion)
        {
            _errorMessage = $"Masa {updatedTable.TableNumber} başka bir terminal tarafından güncellendi. Lütfen ekranı yenileyiniz.";
            return false;
        }

        var index = _tables.FindIndex(t => t.TableId == updatedTable.TableId);
        if (index >= 0)
        {
            _tables[index] = updatedTable;
            if (_selectedTable?.TableId == updatedTable.TableId)
            {
                _selectedTable = updatedTable;
            }
        }
        else
        {
            _tables.Add(updatedTable);
        }

        _errorMessage = null;
        return true;
    }

    public void ClearError()
    {
        _errorMessage = null;
    }
}
