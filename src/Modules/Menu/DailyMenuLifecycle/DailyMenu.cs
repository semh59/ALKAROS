namespace ALKAROS.Menu.DailyMenuLifecycle;

public sealed class DailyMenu
{
    public Guid Id { get; }
    public DateOnly BusinessDate { get; }
    public DailyMenuStatus Status { get; private set; }
    public DateTimeOffset? OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? ClosedBy { get; private set; }
    public string? Note { get; private set; }
    public int RowVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public DailyMenu(
        Guid id,
        DateOnly businessDate,
        DailyMenuStatus status,
        DateTimeOffset? openedAt,
        DateTimeOffset? closedAt,
        Guid? closedBy,
        string? note,
        int rowVersion,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("DailyMenu id cannot be empty.", nameof(id));

        Id = id;
        BusinessDate = businessDate;
        Status = status;
        OpenedAt = openedAt;
        ClosedAt = closedAt;
        ClosedBy = closedBy;
        Note = note;
        RowVersion = rowVersion;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static DailyMenu Create(DateOnly businessDate, string? note = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new DailyMenu(
            id: Guid.NewGuid(),
            businessDate: businessDate,
            status: DailyMenuStatus.Draft,
            openedAt: null,
            closedAt: null,
            closedBy: null,
            note: note,
            rowVersion: 1,
            createdAt: now,
            updatedAt: now);
    }

    public void Open(DateTimeOffset openedAt)
    {
        if (Status != DailyMenuStatus.Draft)
            throw new InvalidDailyMenuTransitionException($"Cannot open daily menu in status '{Status}'. Only Draft menus can be opened.");

        Status = DailyMenuStatus.Open;
        OpenedAt = openedAt;
        UpdatedAt = DateTimeOffset.UtcNow;
        RowVersion++;
    }

    public void Close(Guid closedBy, DateTimeOffset closedAt)
    {
        if (closedBy == Guid.Empty)
            throw new ArgumentException("ClosedBy staff ID cannot be empty.", nameof(closedBy));

        if (Status == DailyMenuStatus.Closed)
            throw new InvalidDailyMenuTransitionException("Daily menu is already closed.");

        Status = DailyMenuStatus.Closed;
        ClosedBy = closedBy;
        ClosedAt = closedAt;
        UpdatedAt = DateTimeOffset.UtcNow;
        RowVersion++;
    }

    public void AssertModifiable()
    {
        if (Status == DailyMenuStatus.Closed)
            throw new DailyMenuClosedException($"Daily menu for date '{BusinessDate:yyyy-MM-dd}' is closed and cannot accept modifications or new operational items.");
    }
}
