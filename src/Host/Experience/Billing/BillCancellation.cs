using ALKAROS.Audit.EventStore;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.Experience.Orders.OrderCancellation;
using ALKAROS.Tables.TableRelease;
using Npgsql;
using NpgsqlTypes;
using System.Data;
using System.Text.Json;

namespace ALKAROS.Host.Experience.Billing;

/// <param name="CancelOrder">Also cancels the order of the bill (every order of a merged bill) (and its stock, portions, kitchen lines, table) in the same transaction.</param>
public sealed record CancelBillRequestV1(string? Reason, bool CancelOrder = false);

public sealed record CancelBillResultV1(
    Guid BillId, string Status, string Reason, DateTimeOffset CancelledAt, bool OrderCancelled = false, Guid? OrderId = null,
    IReadOnlyList<Guid>? CancelledOrderIds = null);

/// <summary>The bill is already paid or money has moved on it; the payment must be refunded first (PO 2026-10-13).</summary>
public sealed class BillCancelHasPaymentException(Guid billId)
    : Exception($"Bill {billId} has payments; refund them before cancelling.");

/// <summary>A line of the bill was already served; only someone who may approve refunds cancels such a bill, everyone else takes payment and asks for a refund.</summary>
public sealed class BillCancelHasServedItemsException(Guid billId)
    : Exception($"Bill {billId} has served items; cancelling it needs a refund approver.");

public sealed class BillCancelNotAllowedException(Guid billId, string status)
    : Exception($"Bill {billId} in state {status} cannot be cancelled.");

public sealed class BillCancelReasonException() : Exception("A cancellation reason of 1 to 500 characters is required.");

/// <summary>
/// Cancels an unpaid bill with a mandatory reason. By default the order stays on its table, so the till can open a
/// fresh bill for it (cancelled bills no longer hold the bill number or the order item ids); with
/// <c>CancelOrder</c> the order is cancelled in the same transaction. A bill with a served line is cancelled only for an
/// actor who may approve refunds (<paramref name="actorMayCancelServed"/>).
/// </summary>
public sealed class BillCancellationStore(
    NpgsqlDataSource dataSource,
    IAuditEventStore auditEvents,
    IBillRepository bills,
    ITableReleasePort tables,
    OrderCancellationStore orderCancellation)
{
    public const int MaxReasonLength = 500;

    public async Task<CancelBillResultV1> CancelAsync(
        Guid billId, string? reason, bool cancelOrder, Guid actorId, bool actorMayCancelServed, string correlationId, CancellationToken cancellationToken)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxReasonLength)
            throw new BillCancelReasonException();

        var now = DateTimeOffset.UtcNow;
        string previousStatus;
        var orderCancelled = false;
        Guid? orderId;
        var ordersToCancel = new List<Guid>();
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken))
        {
            // With the order flag every bill of every order of the bill is locked, in id order like every other caller, so two
            // cancellations or a hand-off on the same order cannot lock them crosswise. A merged bill has several orders.
            if (cancelOrder)
            {
                await using var find = new NpgsqlCommand(BillOrderLink.OrderIdsOfBill, connection, transaction);
                find.Parameters.Add("bill_id", NpgsqlDbType.Uuid).Value = billId;
                await using var found = await find.ExecuteReaderAsync(cancellationToken);
                while (await found.ReadAsync(cancellationToken))
                    ordersToCancel.Add(found.GetGuid(0));
            }
            await BillMoneyGuard.LockBillsOfOrdersAsync(connection, transaction, [.. ordersToCancel], [], cancellationToken);
            await BillMoneyGuard.LockBillAsync(connection, transaction, billId, cancellationToken);

            await using (var read = new NpgsqlCommand(
                "SELECT status, order_id FROM billing.bills WHERE bill_id = @bill_id FOR UPDATE;", connection, transaction))
            {
                read.Parameters.Add("bill_id", NpgsqlDbType.Uuid).Value = billId;
                await using var reader = await read.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    throw new BillingSplitNotFoundException($"Bill {billId} was not found.");
                previousStatus = reader.GetString(0);
                orderId = reader.IsDBNull(1) ? null : reader.GetGuid(1);
            }

            if (previousStatus == "Cancelled")
                throw new BillCancelNotAllowedException(billId, previousStatus);
            if (previousStatus == "Paid" || await BillMoneyGuard.BillHasMoneyAsync(connection, transaction, billId, cancellationToken))
                throw new BillCancelHasPaymentException(billId);
            if (!actorMayCancelServed && await HasServedItemAsync(connection, transaction, billId, cancellationToken))
                throw new BillCancelHasServedItemsException(billId);

            if (ordersToCancel.Count > 0)
            {
                foreach (var orderToCancel in ordersToCancel)
                    await orderCancellation.CancelInTransactionAsync(
                        orderToCancel, trimmed, expectedRowVersion: null, actorId, correlationId, connection, transaction, cancellationToken);
                orderCancelled = true;
            }
            else
            {
                await bills.CancelBillAsync(billId, trimmed, actorId, now, connection, transaction, cancellationToken);
                await tables.ClearBillPointerAsync(billId, connection, transaction, cancellationToken);
            }

            await auditEvents.AppendAsync(
                new AuditEvent(
                    id: Guid.NewGuid(),
                    eventName: "bill.cancelled",
                    aggregateType: "Bill",
                    aggregateId: billId,
                    actorType: "User",
                    correlationId: correlationId,
                    actorId: actorId,
                    reason: trimmed,
                    beforeStateJson: JsonSerializer.Serialize(new { status = previousStatus }),
                    afterStateJson: JsonSerializer.Serialize(new { status = "Cancelled", orderCancelled })),
                connection, transaction, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        return new CancelBillResultV1(billId, "Cancelled", trimmed, now, orderCancelled, orderId, orderCancelled ? ordersToCancel : null);
    }

    private static async Task<bool> HasServedItemAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid billId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1 FROM billing.bill_items bli
                JOIN orders.order_items oli ON oli.order_item_id = bli.order_item_id
                WHERE bli.bill_id = @bill_id AND oli.status = 'Active' AND oli.kitchen_state = 'Served');
            """, connection, transaction);
        command.Parameters.Add("bill_id", NpgsqlDbType.Uuid).Value = billId;
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
