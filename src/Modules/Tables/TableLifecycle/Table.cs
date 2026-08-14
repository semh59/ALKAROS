namespace ALKAROS.Tables.TableLifecycle;

/// <summary>
/// A table row (table_mgmt.tables, PDF:III.5.2). The immutable
/// <see cref="TransitionTo"/> method enforces the canonical transition set
/// (PDF:II.5.15, lifecycle-transition-contracts.md Table row). Current order
/// and bill columns are soft cache pointers only; ownership truth lives in
/// orders.orders.table_id / billing.bills.table_id.
/// </summary>
public sealed class Table
{
    public Table(
        Guid id,
        string tableNumber,
        Guid? zoneId = null,
        int capacity = 0,
        bool active = true,
        TableState state = TableState.Available,
        Guid? currentOrderId = null,
        Guid? currentBillId = null,
        long rowVersion = 1)
    {
        if (string.IsNullOrWhiteSpace(tableNumber))
            throw new ArgumentException("Table number cannot be empty.", nameof(tableNumber));
        if (capacity < 0)
            throw new ArgumentException("Capacity cannot be negative.", nameof(capacity));

        Id = id;
        TableNumber = tableNumber;
        ZoneId = zoneId;
        Capacity = capacity;
        Active = active;
        State = state;
        CurrentOrderId = currentOrderId;
        CurrentBillId = currentBillId;
        RowVersion = rowVersion;
    }

    public Guid Id { get; }

    public string TableNumber { get; }

    public Guid? ZoneId { get; }

    public int Capacity { get; }

    public bool Active { get; }

    public TableState State { get; }

    public Guid? CurrentOrderId { get; }

    public Guid? CurrentBillId { get; }

    public long RowVersion { get; }

    /// <summary>
    /// Returns whether <paramref name="target"/> can immediately follow the
    /// current state according to the canonical Table transition matrix.
    /// </summary>
    public bool CanTransitionTo(TableState target) => target switch
    {
        TableState.Available => State is TableState.Occupied or TableState.Reserved or TableState.Cleaning or TableState.OutOfService,
        TableState.Occupied => State is TableState.Available,
        TableState.Reserved => State is TableState.Available or TableState.Occupied,
        TableState.Cleaning => State is TableState.Available or TableState.OutOfService,
        TableState.OutOfService => State is TableState.Available,
        _ => false,
    };

    /// <summary>
    /// Returns a new instance with the given state when the transition is
    /// allowed; otherwise throws. Row version is preserved here and only
    /// bumped by the repository commit.
    /// </summary>
    public Table TransitionTo(TableState target)
    {
        if (!CanTransitionTo(target))
            throw new InvalidOperationException(
                $"Table {Id} cannot transition from {State} to {target}.");

        return new Table(Id, TableNumber, ZoneId, Capacity, Active, target, CurrentOrderId, CurrentBillId, RowVersion);
    }

    /// <summary>
    /// Returns a copy with the row version advanced; used by repositories
    /// after a successful optimistic concurrency update.
    /// </summary>
    public Table WithRowVersion(long rowVersion)
        => new(Id, TableNumber, ZoneId, Capacity, Active, State, CurrentOrderId, CurrentBillId, rowVersion);
}