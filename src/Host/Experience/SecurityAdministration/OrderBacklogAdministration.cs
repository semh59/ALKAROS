using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Host.Experience.Orders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;

namespace ALKAROS.Host.Experience.SecurityAdministration;

/// <summary>
/// V1-RMD-284: history clean-up for V1-RMD-282. Before that change nothing ever closed an order, so every old
/// order whose check was long paid still sits at Submitted (and skews waiter load, server hand-off and the
/// notes retention). This is a manager-only, deliberately two-step tool on the security administration group:
///
/// <c>GET  .../orders/backlog</c> is READ-ONLY: how many live orders exist, how many are provably settled (every
/// bill Paid or Cancelled, at least one Paid), how many have no bill at all, how many have a bill still open.
/// <c>POST .../orders/close-settled</c> closes ONLY the provably settled ones, through the same
/// OrderSettlementService a fresh payment uses, in bounded batches. <c>dryRun=true</c> (the DEFAULT) changes
/// nothing and lists what would close. Orders with no bill or an open bill are never touched: closing those is a
/// business decision, not a data repair.
/// </summary>
public static class OrderBacklogAdministration
{
    public const int DefaultBatch = 200;
    public const int MaxBatch = 1000;

    /// <summary>A fixed, non-empty id so every batch of this tool shares one audit aggregate.</summary>
    public static readonly Guid AuditAggregateId = Guid.Parse("00000000-0000-0000-0000-000000000284");

    private const string LiveStatuses = "('Submitted', 'Accepted', 'Preparing', 'Ready', 'Served')";

    public static RouteGroupBuilder MapOrderBacklog(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/orders/backlog", async (NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
        {
            await using var command = dataSource.CreateCommand(
                $"""
                SELECT count(*) AS live,
                       count(*) FILTER (WHERE settled) AS settled,
                       count(*) FILTER (WHERE NOT has_bill) AS without_bill,
                       count(*) FILTER (WHERE has_bill AND NOT settled) AS with_open_bill,
                       min(created_at) FILTER (WHERE settled) AS oldest_settled
                FROM (
                    SELECT o.created_at,
                           EXISTS (SELECT 1 FROM billing.bills b WHERE b.order_id = o.order_id) AS has_bill,
                           (EXISTS (SELECT 1 FROM billing.bills b WHERE b.order_id = o.order_id AND b.status = 'Paid')
                            AND NOT EXISTS (SELECT 1 FROM billing.bills b
                                            WHERE b.order_id = o.order_id AND b.status NOT IN ('Paid', 'Cancelled'))) AS settled
                    FROM orders.orders o
                    WHERE o.status IN {LiveStatuses}
                ) live_orders;
                """);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            return Results.Ok(new OrderBacklogV1(
                reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3),
                reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4)));
        });

        group.MapPost("/orders/close-settled", async (
            bool? dryRun,
            int? limit,
            NpgsqlDataSource dataSource,
            OrderSettlementService settlement,
            IAuditEventStore auditEvents,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = SecurityAdministrationEndpointFilter.RequireActorId(context);
            var isDryRun = dryRun ?? true;
            var batch = Math.Clamp(limit ?? DefaultBatch, 1, MaxBatch);

            var candidates = new List<(Guid OrderId, string OrderNumber, Guid PaidBillId)>();
            await using (var command = dataSource.CreateCommand(
                $"""
                SELECT o.order_id, o.order_number,
                       (SELECT b.bill_id FROM billing.bills b
                        WHERE b.order_id = o.order_id AND b.status = 'Paid' ORDER BY b.opened_at LIMIT 1)
                FROM orders.orders o
                WHERE o.status IN {LiveStatuses}
                  AND EXISTS (SELECT 1 FROM billing.bills b WHERE b.order_id = o.order_id AND b.status = 'Paid')
                  AND NOT EXISTS (SELECT 1 FROM billing.bills b
                                  WHERE b.order_id = o.order_id AND b.status NOT IN ('Paid', 'Cancelled'))
                ORDER BY o.created_at
                LIMIT @batch;
                """))
            {
                command.Parameters.AddWithValue("batch", batch);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    candidates.Add((reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2)));
            }

            var sample = candidates.Take(20).Select(c => c.OrderNumber).ToArray();
            if (isDryRun)
                return Results.Ok(new CloseSettledOrdersResultV1(true, candidates.Count, 0, 0, sample));

            var closed = new List<Guid>();
            var failed = 0;
            foreach (var candidate in candidates)
            {
                try
                {
                    if (await settlement.CompleteForPaidBillAsync(candidate.PaidBillId, cancellationToken))
                        closed.Add(candidate.OrderId);
                    else
                        failed++;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failed++;
                }
            }

            await auditEvents.AppendAsync(
                new AuditEvent(
                    id: Guid.NewGuid(),
                    eventName: "orders.backfill.closed-settled",
                    aggregateType: "OrderBacklog",
                    aggregateId: AuditAggregateId,
                    actorType: "User",
                    correlationId: context.TraceIdentifier,
                    actorId: actorId,
                    reason: "Ödemesi tamamlanmış eski siparişler kapatıldı.",
                    afterStateJson: JsonSerializer.Serialize(new
                    {
                        requested = candidates.Count,
                        closed = closed.Count,
                        failed,
                        orderIds = closed.Take(100).ToArray(),
                    })),
                cancellationToken);
            return Results.Ok(new CloseSettledOrdersResultV1(false, candidates.Count, closed.Count, failed, sample));
        });

        return group;
    }
}

public sealed record OrderBacklogV1(
    long LiveOrders, long ProvablySettled, long WithoutBill, long WithOpenBill, DateTimeOffset? OldestSettledCreatedAt);

public sealed record CloseSettledOrdersResultV1(bool DryRun, int Eligible, int Closed, int Failed, IReadOnlyList<string> SampleOrderNumbers);
