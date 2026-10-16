using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Host.Composition.Errors;
using ALKAROS.Host.Experience.Observability;
using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Identity.Authorization;
using ALKAROS.Observability.ErrorNotifications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ALKAROS.Host.Observability.ErrorEventEndpoints;

public sealed record ErrorEventListItem(
    string Fingerprint, string Layer, string Severity, string Category, string? Detail, string Status,
    long OccurrenceCount, DateTimeOffset FirstSeenAt, DateTimeOffset LastSeenAt, string? TerminalLabel, bool Synthetic);

public sealed record ErrorOccurrenceItem(DateTimeOffset OccurredAt, string Severity, int Occurrences, string? TraceId);

public sealed record ErrorEventDetail(ErrorEventListItem Event, IReadOnlyList<ErrorOccurrenceItem> Recent);

public sealed record ErrorEventPage(IReadOnlyList<ErrorEventListItem> Items, long Total, int Page, int PageSize);

/// <summary>Read and status-change queries for the management error list; the list is paged and filtered in SQL.</summary>
public sealed class ErrorEventQueries(NpgsqlDataSource dataSource)
{
    public const int MaxPageSize = 100;

    private static readonly HashSet<string> Layers = new(StringComparer.Ordinal)
        { "Database", "Api", "Background", "Printer", "Payment", "Kitchen", "Integration", "Browser", "Security", "Backup" };
    private static readonly HashSet<string> Severities = new(StringComparer.Ordinal) { "Info", "Warning", "Error", "Critical" };
    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal) { "Open", "Acknowledged", "Resolved" };

    public async Task<ErrorEventPage> ListAsync(
        string? layer, string? severity, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var where = new List<string>();
        var command = dataSource.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            AddFilter(where, command, "layer", layer, Layers);
            AddFilter(where, command, "severity", severity, Severities);
            AddFilter(where, command, "status", status, Statuses);
            var clause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;
            command.CommandText =
                $"""
                SELECT fingerprint, layer, severity, category, detail, status, occurrence_count,
                       first_seen_at, last_seen_at, last_terminal_label, synthetic, count(*) OVER ()
                FROM observability.error_events {clause}
                ORDER BY last_seen_at DESC
                LIMIT @limit OFFSET @offset;
                """;
            command.Parameters.AddWithValue("limit", pageSize);
            command.Parameters.AddWithValue("offset", (page - 1) * pageSize);
            var items = new List<ErrorEventListItem>();
            long total = 0;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(Map(reader));
                total = reader.GetInt64(11);
            }

            return new ErrorEventPage(items, total, page, pageSize);
        }
    }

    public async Task<ErrorEventDetail?> GetAsync(string fingerprint, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT fingerprint, layer, severity, category, detail, status, occurrence_count,
                   first_seen_at, last_seen_at, last_terminal_label, synthetic, 1
            FROM observability.error_events WHERE fingerprint = @f;
            """);
        command.Parameters.AddWithValue("f", fingerprint);
        ErrorEventListItem item;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return null;
            item = Map(reader);
        }

        await using var recent = dataSource.CreateCommand(
            """
            SELECT occurred_at, severity, occurrences, trace_id FROM observability.error_occurrences
            WHERE fingerprint = @f ORDER BY occurred_at DESC LIMIT 20;
            """);
        recent.Parameters.AddWithValue("f", fingerprint);
        var rows = new List<ErrorOccurrenceItem>();
        await using var occurrences = await recent.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await occurrences.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ErrorOccurrenceItem(
                Utc(occurrences.GetDateTime(0)), occurrences.GetString(1), occurrences.GetInt32(2),
                occurrences.IsDBNull(3) ? null : occurrences.GetString(3)));
        }

        return new ErrorEventDetail(item, rows);
    }

    /// <summary>Open → Acknowledged; returns false when the error is unknown or not open.</summary>
    public async Task<bool> AcknowledgeAsync(string fingerprint, Guid actorId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            UPDATE observability.error_events
            SET status = 'Acknowledged', acknowledged_by = @actor, acknowledged_at = now()
            WHERE fingerprint = @f AND status = 'Open';
            """);
        command.Parameters.AddWithValue("actor", actorId);
        command.Parameters.AddWithValue("f", fingerprint);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    /// <summary>Open or Acknowledged → Resolved; returns false when unknown or already resolved.</summary>
    public async Task<bool> ResolveAsync(string fingerprint, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE observability.error_events SET status = 'Resolved', resolved_at = now() WHERE fingerprint = @f AND status <> 'Resolved';");
        command.Parameters.AddWithValue("f", fingerprint);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    private static void AddFilter(List<string> where, NpgsqlCommand command, string column, string? value, HashSet<string> allowed)
    {
        if (string.IsNullOrEmpty(value) || !allowed.Contains(value))
            return;
        where.Add($"{column} = @{column}");
        command.Parameters.AddWithValue(column, value);
    }

    private static ErrorEventListItem Map(NpgsqlDataReader reader) => new(
        reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetInt64(6),
        Utc(reader.GetDateTime(7)), Utc(reader.GetDateTime(8)),
        reader.IsDBNull(9) ? null : reader.GetString(9), reader.GetBoolean(10));

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

public static class ErrorEventEndpointsExtensions
{
    public static IServiceCollection AddErrorEventEndpoints(this IServiceCollection services)
    {
        services.AddSingleton(sp => new ErrorEventQueries(sp.GetRequiredService<MonitoringDataSource>().Inner));
        return services;
    }

    public static IEndpointRouteBuilder MapErrorEventEndpoints(this IEndpointRouteBuilder app)
    {
        ApiErrorHandling.EnsureFor(app);
        var group = app.MapGroup("/api/v1/management/observability/errors");
        group.AddEndpointFilter<ObservabilityEndpointFilter>();

        group.MapGet("/", async (
            string? layer, string? severity, string? status, int? page, int? pageSize,
            ErrorEventQueries queries, CancellationToken cancellationToken) =>
            Results.Ok(await queries.ListAsync(layer, severity, status, page ?? 1, pageSize ?? 50, cancellationToken)));

        group.MapGet("/{fingerprint}", async (string fingerprint, ErrorEventQueries queries, CancellationToken cancellationToken) =>
            await queries.GetAsync(fingerprint, cancellationToken) is { } detail ? Results.Ok(detail) : Results.NotFound());

        group.MapGet("/notification-channel", ([FromServices] INotificationChannel channel) =>
            Results.Ok(new { configured = channel.IsConfigured }));

        group.MapPost("/{fingerprint}/acknowledge", (
            string fingerprint, ErrorEventQueries queries, [FromServices] IAuthorizationService authorization, [FromServices] IAuditEventStore audit,
            HttpContext context, CancellationToken cancellationToken) =>
            ChangeAsync(fingerprint, "acknowledged", context, authorization, audit,
                actorId => queries.AcknowledgeAsync(fingerprint, actorId, cancellationToken), cancellationToken));

        group.MapPost("/{fingerprint}/resolve", (
            string fingerprint, ErrorEventQueries queries, [FromServices] IAuthorizationService authorization, [FromServices] IAuditEventStore audit,
            HttpContext context, CancellationToken cancellationToken) =>
            ChangeAsync(fingerprint, "resolved", context, authorization, audit,
                _ => queries.ResolveAsync(fingerprint, cancellationToken), cancellationToken));
        return app;
    }

    private static async Task<IResult> ChangeAsync(
        string fingerprint, string action, HttpContext context, IAuthorizationService authorization, IAuditEventStore audit,
        Func<Guid, Task<bool>> change, CancellationToken cancellationToken)
    {
        var actorId = ObservabilityEndpointFilter.RequireActorId(context);
        await authorization.AuthorizeAsync(actorId, ObservabilityEndpoints.ManagePermission, cancellationToken);
        if (!await change(actorId))
            return Results.NotFound();

        await audit.AppendAsync(
            new AuditEvent(
                Guid.NewGuid(), $"monitoring.error.{action}", "ErrorEvent", Guid.NewGuid(), "User", Guid.NewGuid().ToString("N"),
                actorId: actorId, reason: "Management error list",
                afterStateJson: JsonSerializer.Serialize(new { fingerprint })),
            cancellationToken);
        return Results.NoContent();
    }
}
