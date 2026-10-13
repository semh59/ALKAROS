using ALKAROS.Audit.EventStore;
using Npgsql;
using NpgsqlTypes;
using System.Data;
using System.Text.Json;

namespace ALKAROS.Host.Experience.Billing;

public sealed record CancelBillRequestV1(string? Reason);

public sealed record CancelBillResultV1(Guid BillId, string Status, string Reason, DateTimeOffset CancelledAt);

/// <summary>The bill is already paid or money has moved on it; the payment must be refunded first (PO 2026-10-13).</summary>
public sealed class BillCancelHasPaymentException(Guid billId)
    : Exception($"Bill {billId} has payments; refund them before cancelling.");

public sealed class BillCancelNotAllowedException(Guid billId, string status)
    : Exception($"Bill {billId} in state {status} cannot be cancelled.");

public sealed class BillCancelReasonException() : Exception("A cancellation reason of 1 to 500 characters is required.");

/// <summary>
/// Cancels an unpaid bill with a mandatory reason. The order stays on its table, so the till can open a
/// fresh bill for it (cancelled bills no longer hold the bill number or the order item ids).
/// </summary>
public sealed class BillCancellationStore(NpgsqlDataSource dataSource, IAuditEventStore auditEvents)
{
    public const int MaxReasonLength = 500;

    public async Task<CancelBillResultV1> CancelAsync(
        Guid billId, string? reason, Guid actorId, string correlationId, CancellationToken cancellationToken)
    {
        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxReasonLength)
            throw new BillCancelReasonException();

        var now = DateTimeOffset.UtcNow;
        string previousStatus;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken))
        {
            await using (var lockCommand = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", connection, transaction))
            {
                lockCommand.Parameters.AddWithValue($"bill-settlement:{billId:N}");
                await lockCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var read = new NpgsqlCommand(
                "SELECT status FROM billing.bills WHERE bill_id = @bill_id FOR UPDATE;", connection, transaction))
            {
                read.Parameters.Add("bill_id", NpgsqlDbType.Uuid).Value = billId;
                previousStatus = await read.ExecuteScalarAsync(cancellationToken) as string
                    ?? throw new BillingSplitNotFoundException($"Bill {billId} was not found.");
            }

            if (previousStatus == "Cancelled")
                throw new BillCancelNotAllowedException(billId, previousStatus);
            if (previousStatus == "Paid" || await HasMoneyAsync(connection, transaction, billId, cancellationToken))
                throw new BillCancelHasPaymentException(billId);

            await using (var cancel = new NpgsqlCommand(
                """
                UPDATE billing.bill_items SET superseded_at = @now WHERE bill_id = @bill_id AND superseded_at IS NULL;

                UPDATE billing.bills
                SET status = 'Cancelled', cancelled_at = @now, updated_at = @now,
                    cancellation_reason = @reason, cancelled_by = @actor, row_version = row_version + 1
                WHERE bill_id = @bill_id;

                UPDATE table_mgmt.tables
                SET current_bill_id = NULL, row_version = row_version + 1
                WHERE current_bill_id = @bill_id;
                """, connection, transaction))
            {
                cancel.Parameters.Add("bill_id", NpgsqlDbType.Uuid).Value = billId;
                cancel.Parameters.Add("now", NpgsqlDbType.TimestampTz).Value = now;
                cancel.Parameters.Add("reason", NpgsqlDbType.Text).Value = trimmed;
                cancel.Parameters.Add("actor", NpgsqlDbType.Uuid).Value = actorId;
                await cancel.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
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
                afterStateJson: JsonSerializer.Serialize(new { status = "Cancelled" })),
            cancellationToken);

        return new CancelBillResultV1(billId, "Cancelled", trimmed, now);
    }

    // Same predicate as CashierHandoffStore.HasMoneyAsync: an allocation, or a payment still in flight; a
    // payment that has been refunded in full no longer holds money.
    private static async Task<bool> HasMoneyAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid billId, CancellationToken cancellationToken)
    {
        await using var money = new NpgsqlCommand(
            """
            SELECT EXISTS (SELECT 1 FROM payments.payment_allocations a
                           JOIN payments.payments ap ON ap.payment_id = a.payment_id
                           WHERE a.bill_id = @bill_id AND ap.status <> 'Refunded')
                OR EXISTS (SELECT 1 FROM payments.payments p
                           WHERE p.bill_id = @bill_id AND p.status IN ('Pending', 'Unknown', 'ReconciliationRequired'));
            """, connection, transaction);
        money.Parameters.Add("bill_id", NpgsqlDbType.Uuid).Value = billId;
        return (bool)(await money.ExecuteScalarAsync(cancellationToken))!;
    }
}
