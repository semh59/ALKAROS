using System.Text.Json;
using ALKAROS.Audit.EventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;

namespace ALKAROS.Host.Experience.SecurityAdministration;

/// <summary>
/// V1-RMD-288: <c>OutboxDispatcherHostedService</c> only ever logs a dead-lettered integration event — a
/// consumer that permanently fails (a table-transfer's order or bill consumer, say) drops its event with
/// nothing but a log line an operator has to already be watching for. This is a manager-only, read-then-act
/// pair on the security administration group, same two-step shape as <see cref="OrderBacklogAdministration"/>:
///
/// <c>GET  .../outbox/dead-letters</c> is READ-ONLY: the dead-lettered rows (event type, aggregate, attempt
/// count, last error, when it died), newest first.
/// <c>POST .../outbox/dead-letters/{id}/requeue</c> puts ONE message back to pending with a reset attempt
/// counter, so the dispatcher picks it up on its next poll. <c>dryRun=true</c> (the DEFAULT) changes nothing
/// and only confirms the message is actually dead. Fixing why the consumer failed in the first place is out
/// of scope - requeuing a message whose root cause was never fixed will just dead-letter it again.
/// </summary>
public static class OutboxAdministration
{
    /// <summary>A fixed, non-empty id so every requeue of this tool shares one audit aggregate.</summary>
    public static readonly Guid AuditAggregateId = Guid.Parse("00000000-0000-0000-0000-000000000288");

    public static RouteGroupBuilder MapOutbox(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/outbox/dead-letters", async (
            int? limit, NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
        {
            var take = Math.Clamp(limit ?? 100, 1, 500);
            var items = new List<OutboxDeadLetterV1>();
            await using (var command = dataSource.CreateCommand(
                """
                SELECT id, event_type, aggregate_type, aggregate_id, attempt_count, last_error, created_at
                FROM outbox_messages
                WHERE status = 'dead'
                ORDER BY created_at DESC
                LIMIT @take;
                """))
            {
                command.Parameters.AddWithValue("take", take);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    items.Add(new OutboxDeadLetterV1(
                        reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetGuid(3),
                        reader.GetInt32(4), reader.IsDBNull(5) ? null : reader.GetString(5),
                        new DateTimeOffset(reader.GetDateTime(6))));
            }

            await using var countCommand = dataSource.CreateCommand(
                "SELECT count(*) FROM outbox_messages WHERE status = 'dead';");
            var total = (long)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0L);

            return Results.Ok(new OutboxDeadLetterListV1(total, items));
        });

        group.MapPost("/outbox/dead-letters/{id:guid}/requeue", async (
            Guid id,
            bool? dryRun,
            NpgsqlDataSource dataSource,
            IAuditEventStore auditEvents,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = SecurityAdministrationEndpointFilter.RequireActorId(context);
            var isDryRun = dryRun ?? true;

            string eventType;
            string aggregateType;
            Guid aggregateId;
            int attemptCount;
            await using (var readCommand = dataSource.CreateCommand(
                """
                SELECT event_type, aggregate_type, aggregate_id, attempt_count
                FROM outbox_messages
                WHERE id = @id AND status = 'dead';
                """))
            {
                readCommand.Parameters.AddWithValue("id", id);
                await using var reader = await readCommand.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    return Results.Json(
                        new SecurityAdministrationApiErrorEnvelopeV1(new SecurityAdministrationApiErrorV1(
                            "NOT_FOUND", "Ölü mektup bulunamadı ya da zaten yeniden kuyruğa alınmış.",
                            StatusCodes.Status404NotFound, context.TraceIdentifier)),
                        statusCode: StatusCodes.Status404NotFound);
                eventType = reader.GetString(0);
                aggregateType = reader.GetString(1);
                aggregateId = reader.GetGuid(2);
                attemptCount = reader.GetInt32(3);
            }

            if (isDryRun)
                return Results.Ok(new OutboxRequeueResultV1(true, id, eventType, aggregateType, aggregateId, attemptCount, false));

            await using (var updateCommand = dataSource.CreateCommand(
                """
                UPDATE outbox_messages
                SET status = 'pending', attempt_count = 0, next_retry_at = NULL, claimed_at = NULL
                WHERE id = @id AND status = 'dead';
                """))
            {
                updateCommand.Parameters.AddWithValue("id", id);
                var affected = await updateCommand.ExecuteNonQueryAsync(cancellationToken);
                if (affected != 1)
                    return Results.Json(
                        new SecurityAdministrationApiErrorEnvelopeV1(new SecurityAdministrationApiErrorV1(
                            "NOT_FOUND", "Ölü mektup bulunamadı ya da zaten yeniden kuyruğa alınmış.",
                            StatusCodes.Status404NotFound, context.TraceIdentifier)),
                        statusCode: StatusCodes.Status404NotFound);
            }

            await auditEvents.AppendAsync(
                new AuditEvent(
                    id: Guid.NewGuid(),
                    eventName: "outbox.dead-letter.requeued",
                    aggregateType: "OutboxDeadLetter",
                    aggregateId: AuditAggregateId,
                    actorType: "User",
                    correlationId: context.TraceIdentifier,
                    actorId: actorId,
                    reason: "Kalıcı başarısız olmuş entegrasyon olayı yeniden kuyruğa alındı.",
                    afterStateJson: JsonSerializer.Serialize(new
                    {
                        messageId = id, eventType, aggregateType, aggregateId, priorAttemptCount = attemptCount,
                    })),
                cancellationToken);

            return Results.Ok(new OutboxRequeueResultV1(false, id, eventType, aggregateType, aggregateId, attemptCount, true));
        });

        return group;
    }
}

public sealed record OutboxDeadLetterV1(
    Guid Id, string EventType, string AggregateType, Guid AggregateId,
    int AttemptCount, string? LastError, DateTimeOffset CreatedAt);

public sealed record OutboxDeadLetterListV1(long Total, IReadOnlyList<OutboxDeadLetterV1> Items);

public sealed record OutboxRequeueResultV1(
    bool DryRun, Guid Id, string EventType, string AggregateType, Guid AggregateId,
    int PriorAttemptCount, bool Requeued);
