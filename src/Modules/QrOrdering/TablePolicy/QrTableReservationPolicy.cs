using ALKAROS.Tables.TableLifecycle;
using Npgsql;

namespace ALKAROS.QrOrdering.TablePolicy;

/// <summary>
/// V12-QRO-002, relaxed by V12-QRO-004. The actual anti-remote-abuse
/// mechanism the task exists for: a QR code, unlike an NFC tap, can be
/// photographed and reused from anywhere (docs/design/modules/
/// qr-nfc-ordering.md §1 cites a real, documented $60,000 fraud case built
/// on exactly that). Without this check, anyone who ever captured a table's
/// QR image could remotely submit an order against a table nobody has even
/// sat down at yet, or before the first order there was ever confirmed —
/// this is what "the table must not already be mid-hand-off" defends
/// against. Reserves the table for a new submission in the SAME transaction
/// as the caller's own idempotency-ledger write and outbox enqueue
/// (table-reservation-policy.md's "current_status always converges to its
/// owner's state within the same transaction that moves the owner").
///
/// V12-QRO-004 (Semih's decision, 2026-09-12: two separate guests should
/// not be forced to share one phone to order): a table that is already
/// <c>Occupied</c> —
/// a waiter has confirmed at least one order there, real guests are
/// genuinely being served — is no longer refused. This was the actual gap:
/// the original all-non-Available-refused rule made a SECOND phone at the
/// SAME real table (a second guest ordering independently once the table
/// opened) indistinguishable from a stranger replaying an old QR photo
/// against someone else's occupied table, and refused both identically.
/// `docs/design/modules/qr-nfc-ordering.md` §6 already documents that this
/// codebase deliberately supports multiple independent orderers at one
/// table and accepts that a live QR code, once genuinely in play at an
/// occupied table, cannot be distinguished from a second real guest by
/// software alone — the fix here makes the code match that already-written,
/// already-accepted decision, and now matches
/// <see cref="ALKAROS.Host.Experience.NfcOrdering.NfcOrderingStore"/>'s own
/// identical Available-or-Occupied rule (NFC has allowed exactly this since
/// V12-NFC-001; only QR's own separate reservation policy had never been
/// brought in line with it).
///
/// A table that is <c>Reserved</c> (an earlier submission is still awaiting
/// a waiter's confirmation — no Order has been accepted yet, so there is no
/// "genuinely occupied" table to add to), <c>Cleaning</c> or
/// <c>OutOfService</c> is still refused outright: none of those describe a
/// table any submission — first or second — should ever be able to touch.
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

        // A table already genuinely occupied accepts another independent QR
        // submission (V12-QRO-004) exactly like NfcOrderingStore already
        // does — table state is left untouched, no re-reservation needed,
        // the new order simply materializes alongside the existing one
        // (QrOrderSubmittedConsumer already keys each order by its own
        // SubmissionId, and Tables.LinkCurrentOrderAsync's own
        // "IS NULL OR = @order_id" guard already never clobbers an
        // in-flight order's pointer with a second one's).
        if (table.State == TableState.Occupied)
            return;

        // Every other non-Available state (Reserved by an unconfirmed first
        // submission, Cleaning, OutOfService) is still refused outright —
        // deliberately stricter than Table.CanTransitionTo(Reserved), which
        // also permits Occupied -> Reserved at the domain-model level (a
        // general-purpose rule meant for other callers, e.g. the cashier's
        // own manual reservation action against a table that will free up
        // shortly).
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
