using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.SessionLifecycle;
using Npgsql;

namespace ALKAROS.Host.DualScreen;

/// <summary>
/// What a cash close warns about, and the audit trail of closing and reconciling a drawer session. Bills and
/// payments belong to the business, not to a terminal, so the counts are business-wide and only ever warn: the
/// cashier acknowledges them, nothing blocks.
/// </summary>
public sealed class CashCloseStore(NpgsqlDataSource dataSource, IAuditEventStore audit)
{
    /// <summary>Open checks (bills that are neither paid nor cancelled, and sent orders with no bill yet), and payments whose result is not known yet.</summary>
    public sealed record Unsettled(int OpenBills, int UnsettledPayments)
    {
        public bool Any => OpenBills > 0 || UnsettledPayments > 0;
    }

    // A sent order (any live status) whose check was never opened has no bill row yet, so it would otherwise be invisible here; online
    // orders are paid on the platform.
    private static readonly string UnsettledSql =
        $"""
        SELECT (SELECT count(*) FROM billing.bills WHERE status NOT IN ('Paid', 'Cancelled'))
               + (SELECT count(*) FROM orders.orders o
                  WHERE o.status IN ('Submitted', 'Accepted', 'Preparing', 'Ready', 'Served') AND o.source <> 'Online'
                    AND NOT EXISTS (SELECT 1 FROM billing.bills b
                                    WHERE {BillOrderLink.Matches("b", "o.order_id")} AND b.status <> 'Cancelled')),
               (SELECT count(*) FROM payments.payments WHERE status IN ('Pending', 'Unknown', 'ReconciliationRequired'));
        """;

    public Task<Unsettled> ReadUnsettledAsync(CancellationToken cancellationToken)
        => ReadAsync(dataSource.CreateCommand(UnsettledSql), cancellationToken);

    private static Task<Unsettled> ReadUnsettledAsync(ILockedCashSession locked, CancellationToken cancellationToken)
        => ReadAsync(new NpgsqlCommand(UnsettledSql, locked.Connection, locked.Transaction), cancellationToken);

    private static async Task<Unsettled> ReadAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        await using var _ = command;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new Unsettled(checked((int)reader.GetInt64(0)), checked((int)reader.GetInt64(1)));
    }

    /// <summary>
    /// Runs inside the close transaction after the session is updated: the counts are read here, so the audit event
    /// carries what the server saw at the close; an unacknowledged warning rolls the whole close back.
    /// </summary>
    public async Task AuditClosedAsync(
        CashSessionClosedEvent closed, bool acknowledged, decimal tolerance, ILockedCashSession locked,
        CancellationToken cancellationToken)
    {
        var unsettled = await ReadUnsettledAsync(locked, cancellationToken);
        if (unsettled.Any && !acknowledged)
            throw new CashCloseUnsettledNotAcknowledgedException(closed.CashSessionId, unsettled.OpenBills, unsettled.UnsettledPayments);

        await audit.AppendAsync(
            new AuditEvent(
                Guid.NewGuid(), "cash.session.closed", "CashSession", closed.CashSessionId, "User", closed.CashSessionId.ToString("N"),
                closed.ClosedBy, closed.OverrideReason,
                beforeStateJson: JsonSerializer.Serialize(new { status = locked.Record.Snapshot.Status.ToString() }),
                afterStateJson: JsonSerializer.Serialize(new
                {
                    status = "Closed", closed.ExpectedCash, closed.ActualCash, closed.Difference,
                    differenceExplanation = closed.DifferenceExplanation, supervisorOverride = closed.IsSupervisorOverride,
                    overrideBySessionCashier = closed.IsSupervisorOverride && closed.ClosedBy == locked.Record.Snapshot.CashierUserId,
                    closedBy = closed.ClosedBy, tolerance,
                    openBills = unsettled.OpenBills, unsettledPayments = unsettled.UnsettledPayments, unsettledAcknowledged = acknowledged,
                }, AuditJson)),
            locked.Connection, locked.Transaction, cancellationToken);
    }

    public Task AuditReconciledAsync(CashSessionReconciledEvent reconciled, ILockedCashSession locked, CancellationToken cancellationToken)
        => audit.AppendAsync(
            new AuditEvent(
                Guid.NewGuid(), "cash.session.reconciled", "CashSession", reconciled.CashSessionId, "User",
                reconciled.CashSessionId.ToString("N"), reconciled.ReconciledBy, reconciled.Notes,
                beforeStateJson: JsonSerializer.Serialize(new { status = locked.Record.Snapshot.Status.ToString() }),
                afterStateJson: JsonSerializer.Serialize(new
                {
                    status = "Reconciled", reconciledBy = reconciled.ReconciledBy, selfApproved = reconciled.SelfApproved,
                }, AuditJson)),
            locked.Connection, locked.Transaction, cancellationToken);

    private static readonly JsonSerializerOptions AuditJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}
