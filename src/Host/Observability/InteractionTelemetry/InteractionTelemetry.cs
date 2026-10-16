using System.Text.RegularExpressions;
using ALKAROS.Host.DualScreen;
using ALKAROS.Observability.PerformanceMetrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Observability.InteractionTelemetry;

/// <summary>One recorded interaction as the browser reports it: a fixed key and numbers, never text.</summary>
public sealed record InteractionItem(
    string? Key, string? Kind, int? AgeMs, int? DurationMs, int? LongTasks, int? ShiftMilli, string? Outcome);

public sealed record InteractionBatch(IReadOnlyList<InteractionItem>? Items);

public readonly record struct IngestResult(int Accepted, int Rejected, bool RateLimited);

/// <summary>
/// Validates and stores interaction batches. Keys must look like a catalog key (no free text), the number of distinct
/// keys is capped, and each terminal has its own write allowance per minute so a noisy screen cannot crowd out others.
/// </summary>
public sealed partial class InteractionIngest(
    NpgsqlDataSource dataSource, PerformanceRecorder? recorder = null, Func<DateTimeOffset>? clock = null)
{
    public const int MaxItemsPerBatch = 50;
    public const int PerTerminalPerMinute = 120;
    public const int MaxDistinctKeys = 500;
    public const string OtherKey = "other";
    public const string Role = "cashier";

    private static readonly HashSet<string> Kinds = new(StringComparer.Ordinal) { "click", "navigate", "submit" };
    private static readonly HashSet<string> Outcomes = new(StringComparer.Ordinal) { "ok", "error", "cancelled" };

    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly object _gate = new();
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, Queue<DateTimeOffset>> _recent = [];

    public static bool IsCatalogKey(string? key) => key is { Length: <= 64 } && Key().IsMatch(key);

    public async Task<IngestResult> StoreAsync(InteractionBatch batch, Guid terminalId, CancellationToken cancellationToken)
    {
        var items = (batch.Items ?? []).Take(MaxItemsPerBatch).ToList();
        var valid = items.Where(IsValid).ToList();
        var allowed = Allowance(terminalId, valid.Count);
        var accepted = valid.Take(allowed).ToList();
        if (accepted.Count > 0)
            await InsertAsync(accepted, terminalId, cancellationToken).ConfigureAwait(false);

        return new IngestResult(accepted.Count, (batch.Items?.Count ?? 0) - accepted.Count, allowed < valid.Count);
    }

    private static bool IsValid(InteractionItem item) =>
        IsCatalogKey(item.Key) && item.Kind is not null && Kinds.Contains(item.Kind)
        && item.Outcome is not null && Outcomes.Contains(item.Outcome)
        && item.DurationMs is >= 0 and <= 600_000;

    private int Allowance(Guid terminalId, int wanted)
    {
        lock (_gate)
        {
            var now = _clock();
            if (!_recent.TryGetValue(terminalId, out var hits))
                _recent[terminalId] = hits = new Queue<DateTimeOffset>();
            while (hits.Count > 0 && now - hits.Peek() > TimeSpan.FromMinutes(1))
                hits.Dequeue();
            var allowed = Math.Max(0, Math.Min(wanted, PerTerminalPerMinute - hits.Count));
            for (var i = 0; i < allowed; i++)
                hits.Enqueue(now);
            return allowed;
        }
    }

    private string Capped(string key)
    {
        lock (_gate)
        {
            if (_keys.Contains(key))
                return key;
            if (_keys.Count >= MaxDistinctKeys)
                return OtherKey;
            _keys.Add(key);
            return key;
        }
    }

    private async Task InsertAsync(List<InteractionItem> items, Guid terminalId, CancellationToken cancellationToken)
    {
        var keys = items.Select(item => Capped(item.Key!)).ToArray();
        await using var command = dataSource.CreateCommand(
            """
            INSERT INTO observability.raw_ui_interactions
                (occurred_at, terminal_id, role, key, kind, duration_ms, long_task_count, layout_shift_milli, outcome)
            SELECT now() - make_interval(secs => u.age / 1000.0), @terminal, @role, u.key, u.kind, u.duration, u.tasks, u.shift, u.outcome
            FROM unnest(@keys, @kinds, @ages, @durations, @tasks, @shifts, @outcomes) AS u(key, kind, age, duration, tasks, shift, outcome);
            """);
        command.Parameters.AddWithValue("terminal", terminalId);
        command.Parameters.AddWithValue("role", Role);
        command.Parameters.Add(new NpgsqlParameter("keys", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = keys });
        command.Parameters.Add(new NpgsqlParameter("kinds", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = items.Select(i => i.Kind!).ToArray() });
        command.Parameters.Add(new NpgsqlParameter("ages", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = items.Select(i => Math.Clamp(i.AgeMs ?? 0, 0, 300_000)).ToArray() });
        command.Parameters.Add(new NpgsqlParameter("durations", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = items.Select(i => i.DurationMs!.Value).ToArray() });
        command.Parameters.Add(new NpgsqlParameter("tasks", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = items.Select(i => Math.Clamp(i.LongTasks ?? 0, 0, 10_000)).ToArray() });
        command.Parameters.Add(new NpgsqlParameter("shifts", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = items.Select(i => Math.Clamp(i.ShiftMilli ?? 0, 0, 100_000)).ToArray() });
        command.Parameters.Add(new NpgsqlParameter("outcomes", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = items.Select(i => i.Outcome!).ToArray() });
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        if (recorder is not null)
        {
            for (var i = 0; i < items.Count; i++)
                recorder.Record(PerformanceMetric.UiInteraction, keys[i], items[i].DurationMs!.Value, items[i].Outcome == "error");
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._:-]*(@/[a-z0-9._:/-]*)?$")]
    private static partial Regex Key();
}

public static class InteractionTelemetryEndpoints
{
    public const int MaxBodyBytes = 32 * 1024;

    public static IEndpointRouteBuilder MapInteractionTelemetry(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/telemetry/interactions", async (
            HttpContext context,
            DualScreenStore store,
            InteractionIngest ingest,
            CancellationToken cancellationToken) =>
        {
            if (context.Request.ContentLength > MaxBodyBytes)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            var principal = await store.AuthenticateCashierByCookieAsync(
                context.Request.Cookies[DualScreenApplication.CashierCookieName], cancellationToken);
            if (principal is null)
                return Results.StatusCode(StatusCodes.Status401Unauthorized);
            InteractionBatch? batch;
            try
            {
                batch = await context.Request.ReadFromJsonAsync<InteractionBatch>(cancellationToken);
            }
            catch (System.Text.Json.JsonException)
            {
                batch = null;
            }

            if (batch?.Items is null)
                return Results.StatusCode(StatusCodes.Status400BadRequest);

            var result = await ingest.StoreAsync(batch, principal.TerminalId, cancellationToken);
            return result.RateLimited && result.Accepted == 0
                ? Results.StatusCode(StatusCodes.Status429TooManyRequests)
                : Results.StatusCode(StatusCodes.Status204NoContent);
        });
        return app;
    }
}
