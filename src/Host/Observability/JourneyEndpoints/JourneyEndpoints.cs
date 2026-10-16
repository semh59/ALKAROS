using System.Text.RegularExpressions;
using ALKAROS.Host.Composition.Errors;
using ALKAROS.Host.Experience.Observability;
using ALKAROS.Host.Observability.TraceCorrelation;
using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Identity.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ALKAROS.Host.Observability.JourneyEndpoints;

public sealed record ActionSummary(string Key, long Count, double AverageMilliseconds, int MaxMilliseconds, long ErrorCount);

public sealed record RequestSummary(
    string TraceId, string Method, string Route, int StatusCode, DateTimeOffset StartedAt, double DurationMilliseconds,
    int SpanCount, int FailedSpanCount);

public sealed record JourneyStep(string Kind, string Name, double OffsetMilliseconds, double DurationMilliseconds, bool Failed);

public sealed record Journey(
    string TraceId, DateTimeOffset? StartedAt, bool RequestCaptured, int DroppedSpans, IReadOnlyList<JourneyStep> Steps);

public sealed record StaffMovement(DateTimeOffset OccurredAt, Guid? ActorId, string? ActorName, string EventName, string AggregateType);

/// <summary>
/// Read side of the user journey screen. Requests and their database/outgoing-call spans come from the in-memory
/// trace buffer (recent requests of this host process); events come from the audit log by the same trace id;
/// button statistics come from the raw interaction table.
/// </summary>
public sealed partial class JourneyQueries(NpgsqlDataSource dataSource, TraceSpanBuffer buffer)
{
    public const int MaxRows = 50;
    public const int MaxEvents = 100;
    public const int MaxMovements = 200;
    public const int MaxWindowHours = 168;
    public const string OwnRoute = "/api/v1/management/observability/journeys";

    private static readonly HashSet<string> Roles = new(StringComparer.Ordinal) { "cashier", "waiter", "kitchen", "manager" };

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex TraceIdShape();

    public static bool IsTraceId(string value) => TraceIdShape().IsMatch(value);

    public async Task<IReadOnlyList<ActionSummary>> ActionsAsync(
        string sort, string? role, Guid? terminalId, int hours, CancellationToken cancellationToken)
    {
        hours = Math.Clamp(hours, 1, MaxWindowHours);
        var order = sort == "failing" ? "error_count DESC, avg_ms DESC" : "avg_ms DESC, error_count DESC";
        await using var command = dataSource.CreateCommand(
            $"""
            SELECT key, count(*), avg(duration_ms)::float8 AS avg_ms, max(duration_ms), count(*) FILTER (WHERE outcome = 'error') AS error_count
            FROM observability.raw_ui_interactions
            WHERE occurred_at >= now() - make_interval(hours => @hours)
              AND (@role::text IS NULL OR role = @role)
              AND (@terminal::uuid IS NULL OR terminal_id = @terminal)
            GROUP BY key
            ORDER BY {order}
            LIMIT {MaxRows};
            """);
        command.Parameters.AddWithValue("hours", hours);
        command.Parameters.AddWithValue("role", role is not null && Roles.Contains(role) ? role : DBNull.Value);
        command.Parameters.AddWithValue("terminal", terminalId is { } id ? id : DBNull.Value);
        var rows = new List<ActionSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(new ActionSummary(reader.GetString(0), reader.GetInt64(1), reader.GetDouble(2), reader.GetInt32(3), reader.GetInt64(4)));
        return rows;
    }

    public IReadOnlyList<RequestSummary> Requests(string sort, double minMilliseconds)
    {
        var requests = buffer.Recent(TraceSpanBuffer.MaxTraces)
            .Where(t => !t.Route.StartsWith(OwnRoute, StringComparison.Ordinal) && t.DurationMilliseconds >= minMilliseconds)
            .Select(t => new RequestSummary(
                t.TraceId, t.Method, t.Route, t.StatusCode, t.StartedAt, t.DurationMilliseconds, t.Spans.Count, t.Spans.Count(s => s.Failed)));
        var ordered = sort switch
        {
            "slowest" => requests.OrderByDescending(r => r.DurationMilliseconds),
            "failing" => requests.OrderByDescending(r => r.StatusCode >= 500 || r.FailedSpanCount > 0).ThenByDescending(r => r.DurationMilliseconds),
            _ => requests,
        };
        return ordered.Take(MaxRows).ToList();
    }

    public async Task<Journey?> TimelineAsync(string traceId, CancellationToken cancellationToken)
    {
        var record = buffer.Get(traceId);
        var events = await EventsAsync(traceId, cancellationToken).ConfigureAwait(false);
        if (record is null && events.Count == 0)
            return null;

        var origin = record?.StartedAt ?? events.Min(e => e.At);
        var steps = new List<JourneyStep>();
        if (record is not null)
        {
            steps.Add(new JourneyStep("request", $"{record.Method} {record.Route} ({record.StatusCode})", 0, record.DurationMilliseconds, record.StatusCode >= 500));
            steps.AddRange(record.Spans.Select(s => new JourneyStep(s.Kind, s.Name, Offset(origin, s.StartedAt), s.DurationMilliseconds, s.Failed)));
        }

        steps.AddRange(events.Select(e => new JourneyStep("event", e.Name, Offset(origin, e.At), 0, false)));
        return new Journey(traceId, record?.StartedAt, record is not null, record?.DroppedSpans ?? 0, [.. steps.OrderBy(s => s.OffsetMilliseconds)]);
    }

    public async Task<IReadOnlyList<StaffMovement>> StaffMovementsAsync(Guid? actorId, int hours, CancellationToken cancellationToken)
    {
        hours = Math.Clamp(hours, 1, MaxWindowHours);
        await using var command = dataSource.CreateCommand(
            $"""
            SELECT a.occurred_at, a.actor_id, u.display_name, a.event_name, a.aggregate_type
            FROM audit.audit_events a
            LEFT JOIN identity.users u ON u.user_id = a.actor_id
            WHERE a.actor_id IS NOT NULL
              AND a.occurred_at >= now() - make_interval(hours => @hours)
              AND (@actor::uuid IS NULL OR a.actor_id = @actor)
            ORDER BY a.occurred_at DESC
            LIMIT {MaxMovements};
            """);
        command.Parameters.AddWithValue("hours", hours);
        command.Parameters.AddWithValue("actor", actorId is { } id ? id : DBNull.Value);
        var rows = new List<StaffMovement>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new StaffMovement(
                Utc(reader.GetDateTime(0)), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), reader.GetString(4)));
        }

        return rows;
    }

    private async Task<List<(DateTimeOffset At, string Name)>> EventsAsync(string traceId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            $"SELECT occurred_at, event_name FROM audit.audit_events WHERE correlation_id = @trace ORDER BY occurred_at LIMIT {MaxEvents};");
        command.Parameters.AddWithValue("trace", traceId);
        var rows = new List<(DateTimeOffset, string)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add((Utc(reader.GetDateTime(0)), reader.GetString(1)));
        return rows;
    }

    private static double Offset(DateTimeOffset origin, DateTimeOffset at) => Math.Max(0, (at - origin).TotalMilliseconds);

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

public static class JourneyEndpointsExtensions
{
    public static IServiceCollection AddJourneyEndpoints(this IServiceCollection services)
    {
        services.AddSingleton(sp => new JourneyQueries(sp.GetRequiredService<MonitoringDataSource>().Inner, sp.GetRequiredService<TraceSpanBuffer>()));
        return services;
    }

    public static IEndpointRouteBuilder MapJourneyEndpoints(this IEndpointRouteBuilder app)
    {
        ApiErrorHandling.EnsureFor(app);
        var group = app.MapGroup(JourneyQueries.OwnRoute);
        group.AddEndpointFilter<ObservabilityEndpointFilter>();

        group.MapGet("/actions", async (
            string? sort, string? role, Guid? terminalId, int? hours, [FromServices] JourneyQueries queries, CancellationToken cancellationToken) =>
            Results.Ok(await queries.ActionsAsync(sort ?? "slowest", role, terminalId, hours ?? 24, cancellationToken)));

        group.MapGet("/requests", (string? sort, double? minMs, [FromServices] JourneyQueries queries) =>
            Results.Ok(queries.Requests(sort ?? "slowest", minMs ?? 0)));

        group.MapGet("/requests/{traceId}", async (string traceId, [FromServices] JourneyQueries queries, CancellationToken cancellationToken) =>
            JourneyQueries.IsTraceId(traceId) && await queries.TimelineAsync(traceId, cancellationToken) is { } journey
                ? Results.Ok(journey)
                : Results.NotFound());

        group.MapGet("/staff-movements", async (
            Guid? actorId, int? hours, [FromServices] JourneyQueries queries, [FromServices] IAuthorizationService authorization,
            HttpContext context, CancellationToken cancellationToken) =>
        {
            await authorization.AuthorizeAsync(ObservabilityEndpointFilter.RequireActorId(context), ObservabilityEndpoints.ManagePermission, cancellationToken);
            return Results.Ok(await queries.StaffMovementsAsync(actorId, hours ?? 24, cancellationToken));
        });
        return app;
    }
}
