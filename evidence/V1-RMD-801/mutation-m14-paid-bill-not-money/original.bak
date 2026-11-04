using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.Billing;

/// <summary>
/// The per-bill settlement lock and the "has money moved" rule that decide whether a bill or its order may still
/// be cancelled. Every payment path takes the same <c>bill-settlement</c> lock and re-reads the bill under it, so
/// a cancellation that takes the lock first leaves a waiting tender to find the bill cancelled.
/// </summary>
internal static class BillMoneyGuard
{
    public static async Task LockBillAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid billId, CancellationToken cancellationToken)
    {
        await using var lockCommand = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtext($1)::bigint);", connection, transaction);
        lockCommand.Parameters.AddWithValue($"bill-settlement:{billId:N}");
        await lockCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Locks every bill of the order not locked yet, in id order (the order every caller takes).</summary>
    public static async Task LockOrderBillsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orderId,
        HashSet<Guid> lockedBillIds,
        CancellationToken cancellationToken)
    {
        var billIds = new List<Guid>();
        await using (var read = new NpgsqlCommand(
            "SELECT bill_id FROM billing.bills WHERE order_id = @order_id ORDER BY bill_id;", connection, transaction))
        {
            read.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = orderId;
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                billIds.Add(reader.GetGuid(0));
        }

        foreach (var billId in billIds.Where(lockedBillIds.Add))
            await LockBillAsync(connection, transaction, billId, cancellationToken);
    }

    /// <summary>
    /// An allocation, or a payment still in flight, holds money; a payment refunded in full no longer does.
    /// (Recalling a check from the till is stricter: any allocation at all blocks it.)
    /// </summary>
    public static async Task<bool> BillHasMoneyAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid billId, CancellationToken cancellationToken)
    {
        await using var money = new NpgsqlCommand(BillMoneyPredicate("@bill_id"), connection, transaction);
        money.Parameters.Add("bill_id", NpgsqlDbType.Uuid).Value = billId;
        return (bool)(await money.ExecuteScalarAsync(cancellationToken))!;
    }

    /// <summary>True when any bill of the order is Paid or holds money.</summary>
    public static async Task<bool> OrderHasMoneyAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid orderId, CancellationToken cancellationToken)
    {
        await using var money = new NpgsqlCommand(
            $"""
            SELECT EXISTS (
                SELECT 1 FROM billing.bills ob
                WHERE ob.order_id = @order_id
                  AND (ob.status = 'Paid' OR ({BillMoneyPredicate("ob.bill_id")})));
            """, connection, transaction);
        money.Parameters.Add("order_id", NpgsqlDbType.Uuid).Value = orderId;
        return (bool)(await money.ExecuteScalarAsync(cancellationToken))!;
    }

    private static string BillMoneyPredicate(string billId) => $"""
        SELECT EXISTS (SELECT 1 FROM payments.payment_allocations a
                       JOIN payments.payments ap ON ap.payment_id = a.payment_id
                       WHERE a.bill_id = {billId} AND ap.status <> 'Refunded')
            OR EXISTS (SELECT 1 FROM payments.payments p
                       WHERE p.bill_id = {billId} AND p.status IN ('Pending', 'Unknown', 'ReconciliationRequired'))
        """;
}
