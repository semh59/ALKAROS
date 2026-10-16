using System.Collections.Concurrent;
using System.Diagnostics;
using ALKAROS.Host.Observability.BrowserErrors;
using ALKAROS.Host.Observability.ServerDetectorsDeep;
using ALKAROS.Observability.Foundation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ALKAROS.Host.Observability.TraceCorrelation;

public sealed record TraceSpan(string Kind, string Name, DateTimeOffset StartedAt, double DurationMilliseconds, bool Failed);

public sealed record TraceRecord(
    string TraceId, string Method, string Route, int StatusCode, DateTimeOffset StartedAt, double DurationMilliseconds,
    IReadOnlyList<TraceSpan> Spans, int DroppedSpans);

/// <summary>
/// Keeps the spans (database commands and outgoing calls) of the most recent requests in memory, at most
/// <see cref="MaxSpansPerTrace"/> per request; what does not fit is only counted. Nothing here is persisted.
/// </summary>
public sealed class TraceSpanBuffer : IDisposable
{
    public const int MaxSpansPerTrace = 50;
    public const int MaxTraces = 200;

    private readonly ConcurrentDictionary<string, Open> _open = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TraceRecord> _done = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _order = new();
    private readonly ActivityListener _listener;

    public TraceSpanBuffer()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is "Npgsql" or "System.Net.Http",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = OnStopped,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Begin(string traceId) => _open.GetOrAdd(traceId, _ => new Open()).Enter();

    public void Complete(string traceId, string method, string route, int statusCode, DateTimeOffset startedAt, double durationMilliseconds)
    {
        if (!_open.TryGetValue(traceId, out var open) || !open.Leave())
            return;

        _open.TryRemove(traceId, out _);

        var (spans, dropped) = open.Snapshot();
        _done[traceId] = new TraceRecord(traceId, method, route, statusCode, startedAt, durationMilliseconds, spans, dropped);
        _order.Enqueue(traceId);
        while (_order.Count > MaxTraces && _order.TryDequeue(out var oldest))
            _done.TryRemove(oldest, out _);
    }

    public TraceRecord? Get(string traceId) => _done.GetValueOrDefault(traceId);

    public IReadOnlyList<TraceRecord> Recent(int count) =>
        _order.Reverse().Select(id => _done.GetValueOrDefault(id)).OfType<TraceRecord>().Take(count).ToList();

    private void OnStopped(Activity activity)
    {
        if (!_open.TryGetValue(activity.TraceId.ToString(), out var open))
            return;

        // The plan lookup of the slow query planner is monitoring's own work, not part of the request.
        if (activity.Source.Name == "Npgsql" && activity.GetTagItem("db.statement") is string sql
            && sql.StartsWith("EXPLAIN", StringComparison.OrdinalIgnoreCase))
            return;

        var (kind, name) = activity.Source.Name == "Npgsql"
            ? ("database", activity.GetTagItem("db.statement") is string statement ? DbActivityTap.Shape(statement) : "command")
            : ("external", activity.GetTagItem("server.address") is string host ? $"{activity.GetTagItem("http.request.method")} {host}".Trim() : "external call");
        open.Add(new TraceSpan(kind, name, activity.StartTimeUtc, activity.Duration.TotalMilliseconds, activity.Status == ActivityStatusCode.Error));
    }

    public void Dispose() => _listener.Dispose();

    private sealed class Open
    {
        private readonly object _gate = new();
        private readonly List<TraceSpan> _spans = [];
        private int _dropped;
        private int _depth;

        public void Enter() => Interlocked.Increment(ref _depth);

        /// <summary>True when the last request of this trace has finished (a request may call back into the server).</summary>
        public bool Leave() => Interlocked.Decrement(ref _depth) == 0;

        public void Add(TraceSpan span)
        {
            lock (_gate)
            {
                if (_spans.Count < MaxSpansPerTrace)
                    _spans.Add(span);
                else
                    _dropped++;
            }
        }

        public (IReadOnlyList<TraceSpan> Spans, int Dropped) Snapshot()
        {
            lock (_gate)
                return ([.. _spans], _dropped);
        }
    }
}

/// <summary>
/// Gives every request one trace id: the id the browser sent (W3C traceparent) or a new one. The id becomes the
/// request activity (so error events and logs carry it) and the request's TraceIdentifier (so audit records that
/// already use it carry it too), and is returned to the caller as X-Correlation-Id.
/// </summary>
public sealed class TraceCorrelationMiddleware(RequestDelegate next, TraceSpanBuffer buffer)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var own = Activity.Current is null ? StartOwnActivity(context) : null;
        var traceId = Activity.Current!.TraceId.ToString();
        context.TraceIdentifier = traceId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-Id"] = traceId;
            return Task.CompletedTask;
        });
        buffer.Begin(traceId);
        var startedAt = DateTimeOffset.UtcNow;
        var timer = Stopwatch.StartNew();
        using var scope = CorrelationContext.BeginScope(correlationId: traceId);
        try
        {
            await next(context).ConfigureAwait(false);
        }
        finally
        {
            buffer.Complete(
                traceId, context.Request.Method, RouteOf(context), context.Response.StatusCode, startedAt, timer.Elapsed.TotalMilliseconds);
            own?.Stop();
        }
    }

    private static Activity StartOwnActivity(HttpContext context)
    {
        var activity = new Activity("alkaros.request");
        if (ActivityContext.TryParse(context.Request.Headers["traceparent"].ToString(), null, out var parent))
            activity.SetParentId(parent.TraceId, parent.SpanId, parent.TraceFlags);
        return activity.Start();
    }

    private static string RouteOf(HttpContext context) =>
        context.GetEndpoint() is RouteEndpoint { RoutePattern.RawText: { } pattern }
            ? pattern
            : BrowserErrorIngest.RouteTemplate(context.Request.Path);
}

public static class TraceCorrelationExtensions
{
    public static IServiceCollection AddTraceCorrelation(this IServiceCollection services)
    {
        services.AddSingleton<TraceSpanBuffer>();
        services.AddHostedService(sp => new BufferLifetime(sp.GetRequiredService<TraceSpanBuffer>()));
        return services;
    }

    public static IApplicationBuilder UseTraceCorrelation(this IApplicationBuilder application) =>
        application.UseMiddleware<TraceCorrelationMiddleware>();

    private sealed class BufferLifetime(TraceSpanBuffer buffer) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            buffer.Dispose();
            return Task.CompletedTask;
        }
    }
}
