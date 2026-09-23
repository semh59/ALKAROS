using Npgsql;

namespace ALKAROS.Tables.PaymentTopology;

/// <summary>
/// Payment-aware gate for table transfer/merge/unmerge (V13-TBL-001,
/// PDF:I.49/II.2.3/II.3.16/II.5.15/III.5). Replaces the placeholder "any
/// allocated/paid amount blocks" check that V1-TBL-002/V1-TBL-003 shipped
/// with (their own <c>PaymentPolicyRequiredException</c> doc comments name
/// this exact task as the deferred policy owner).
///
/// <para>
/// A Bill's identity never changes across a transfer/merge/unmerge in this
/// domain — only which table currently points at it
/// (<c>table_mgmt.tables.current_bill_id</c>) and, once the resulting
/// <c>TableTransferred</c>/<c>TableMerged</c> outbox event is delivered,
/// <c>billing.bills.table_id</c> itself
/// (<see cref="ALKAROS.Billing.BillFoundation.IBillRepository.ReparentActiveBillsToTableAsync"/>,
/// called only from that event's own consumer — never reachable unless this
/// gate lets the synchronous transfer/merge/unmerge through in the first
/// place, since the event is only enqueued after this gate passes). So an
/// allocation (<c>billing.bill_allocations</c>, keyed by an immutable
/// <c>bill_id</c>) can never end up attributed to the wrong Bill by a
/// transfer/merge/unmerge — this task's own "an allocation never moves to
/// the wrong Bill" acceptance criterion holds by construction, not by an
/// allocation-moving routine this task would otherwise have to write
/// (allocation calculation itself stays out of scope).
/// </para>
///
/// <para>
/// The only real risk this gate exists to close is a Bill whose payment
/// outcome is not yet settled — <c>Pending</c> (a synchronous tender is
/// mid-flight) or <c>Unknown</c>/<c>ReconciliationRequired</c> (a provider
/// timeout with no confirmed outcome yet, CORR:C29 "never guess"). Moving a
/// table out from under either would let the eventual settlement land
/// against a Bill whose table context has silently shifted. A Bill that is
/// merely partially allocated/paid with every known Payment already
/// <c>Approved</c>/<c>Declined</c>/<c>Cancelled</c> is safe to move — its
/// allocations stay exactly where they are.
/// </para>
/// </summary>
public static class PaymentAwareTableTopologyPolicy
{
    private const string PaymentsTable = "payments.payments";

    private static readonly string[] UnsettledStatuses = ["Pending", "Unknown", "ReconciliationRequired"];

    /// <summary>
    /// Throws <see cref="TableTransfer.PaymentPolicyRequiredException"/> if
    /// any of <paramref name="billIds"/> has a Payment currently
    /// <c>Pending</c>, <c>Unknown</c>, or <c>ReconciliationRequired</c>.
    /// Must run inside the caller's own transfer/merge/unmerge transaction,
    /// after the caller's own table-row locks are held, so a payment cannot
    /// be tendered against a bill between this check and the caller's own
    /// commit.
    /// </summary>
    public static async Task EnsureNoUnsettledPaymentAsync(
        IReadOnlyCollection<Guid> billIds,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (billIds.Count == 0)
            return;

        const string sql = $"""
            SELECT bill_id, status
            FROM {PaymentsTable}
            WHERE bill_id = ANY(@bill_ids) AND status = ANY(@unsettled_statuses)
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("bill_ids", billIds.Distinct().ToArray());
        command.Parameters.AddWithValue("unsettled_statuses", UnsettledStatuses);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var billId = reader.GetGuid(0);
            var status = reader.GetString(1);
            throw new TableTransfer.PaymentPolicyRequiredException(
                billId,
                $"Bill '{billId}' has a payment currently '{status}'. Table topology changes are locked until it settles (V13-TBL-001).");
        }
    }

    /// <summary>
    /// Same check, throwing <see cref="TableMerge.PaymentPolicyRequiredException"/>
    /// instead — <see cref="TableTransfer.PaymentPolicyRequiredException"/> and
    /// <see cref="TableMerge.PaymentPolicyRequiredException"/> are separate
    /// types (each module's own pre-existing exception hierarchy,
    /// V1-TBL-002/V1-TBL-003), both already mapped to the same HTTP 409
    /// "DOMAIN_CONFLICT" response in
    /// <c>TableManagementApplication.cs</c> — this task reuses both rather
    /// than introducing a third type real callers would also have to map.
    /// </summary>
    public static async Task EnsureNoUnsettledPaymentForMergeAsync(
        IReadOnlyCollection<Guid> billIds,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        if (billIds.Count == 0)
            return;

        const string sql = $"""
            SELECT bill_id, status
            FROM {PaymentsTable}
            WHERE bill_id = ANY(@bill_ids) AND status = ANY(@unsettled_statuses)
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("bill_ids", billIds.Distinct().ToArray());
        command.Parameters.AddWithValue("unsettled_statuses", UnsettledStatuses);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var billId = reader.GetGuid(0);
            var status = reader.GetString(1);
            throw new TableMerge.PaymentPolicyRequiredException(
                billId,
                $"Bill '{billId}' has a payment currently '{status}'. Table topology changes are locked until it settles (V13-TBL-001).");
        }
    }
}
