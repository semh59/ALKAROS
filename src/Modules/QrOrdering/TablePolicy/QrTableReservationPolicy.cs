using ALKAROS.Tables.TableLifecycle;
using Npgsql;

namespace ALKAROS.QrOrdering.TablePolicy;

/// <summary>
/// V12-QRO-002. The actual anti-remote-abuse mechanism the task exists for:
/// a QR code, unlike an NFC tap, can be photographed and reused from
/// anywhere (docs/design/modules/qr-nfc-ordering.md §1 cites a real,
/// documented $60,000 fraud case built on exactly that). Without this check,
/// anyone who ever captured a table's QR image could remotely submit an
/// order against it forever, including tables that are already genuinely
/// occupied by other guests — a denial-of-service on that table's real
/// service. Reserves the table for a new submission in the SAME transaction
/// as the caller's own idempotency-ledger write and outbox enqueue
/// (table-reservation-policy.md's "current_status always converges to its
/// owner's state within the same transaction that moves the owner"),
/// refusing outright unless the table is Available.
/// </summary>
public sealed class QrTableReservationPolicy
{
    private readonly ITableRepository _tables;

    public QrTableReservationPolicy(ITableRepository tables)
    {
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
    }

    public async Task ReserveForSubmissionAsync(
        Guid tableId,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var table = await _tables.GetByIdForUpdateAsync(tableId, connection, transaction, cancellationToken)
            ?? throw new QrTableNotFoundException(tableId);
        if (!table.Active)
            throw new QrTableNotFoundException(tableId);
        // Deliberately stricter than Table.CanTransitionTo(Reserved), which
        // also permits Occupied -> Reserved at the domain-model level (a
        // general-purpose rule meant for other callers, e.g. the cashier's
        // own manual reservation action against a table that will free up
        // shortly). A QR submission must never touch an already-Occupied,
        // already-Reserved, Cleaning or OutOfService table — every one of
        // those is "no change" per this task's own goal.
        if (table.State != TableState.Available)
            throw new QrTableNotAvailableException(tableId, table.State);

        await _tables.UpdateStatusAsync(
            tableId, TableState.Reserved, table.RowVersion, connection, transaction, cancellationToken);
    }
}

/// <summary>V12-QRO-002: the table does not exist, or is disabled.</summary>
public sealed class QrTableNotFoundException : Exception
{
    public QrTableNotFoundException(Guid tableId) : base($"Table {tableId} was not found.")
    {
        TableId = tableId;
    }

    public Guid TableId { get; }
}

/// <summary>
/// V12-QRO-002: the table is not Available (Occupied by another guest,
/// already Reserved by a pending order or a manual reservation, Cleaning or
/// OutOfService) — a QR submission never claims, interrupts or overwrites
/// another owner's hold on the table.
/// </summary>
public sealed class QrTableNotAvailableException : Exception
{
    public QrTableNotAvailableException(Guid tableId, TableState currentState)
        : base($"Table {tableId} is not available for a QR order (status: {currentState}).")
    {
        TableId = tableId;
        CurrentState = currentState;
    }

    public Guid TableId { get; }
    public TableState CurrentState { get; }
}
