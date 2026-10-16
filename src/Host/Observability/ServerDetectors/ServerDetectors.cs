using System.Diagnostics;
using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Host.Composition.Errors;
using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Observability.AlertFoundation;
using ALKAROS.Observability.ErrorEvents;
using ALKAROS.Observability.TelemetryPrivacy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace ALKAROS.Host.Observability.ServerDetectors;

public sealed record SlowRequestOptions(TimeSpan Threshold)
{
    public static SlowRequestOptions Default { get; } = new(TimeSpan.FromMilliseconds(2000));
}

/// <summary>
/// Records server-side failures as error events without answering the request: it sits in front of the shared
/// API error handler and always passes the exception on. Only 5xx outcomes and database fault codes count; a
/// 4xx is a caller's mistake, not a defect.
/// </summary>
public sealed class ServerErrorObserver(ErrorEventWriter writer) : IExceptionHandler
{
    private static readonly Dictionary<string, (string Category, ErrorSeverity Severity)> DatabaseCodes =
        new Dictionary<string, (string, ErrorSeverity)>(StringComparer.Ordinal)
        {
            ["55P03"] = ("db.lock_not_available", ErrorSeverity.Error),
            ["40P01"] = ("db.deadlock_detected", ErrorSeverity.Error),
            ["53300"] = ("db.too_many_connections", ErrorSeverity.Critical),
            ["57P03"] = ("db.cannot_connect_now", ErrorSeverity.Critical),
            ["08006"] = ("db.connection_failure", ErrorSeverity.Critical),
        };

    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            Observe(httpContext, exception);
        }
        catch (Exception observerFailure) when (observerFailure is not OperationCanceledException)
        {
            // Observing is best effort: the shared handler must still answer the request.
            return ValueTask.FromResult(false);
        }

        return ValueTask.FromResult(false);
    }

    private void Observe(HttpContext httpContext, Exception exception)
    {
        var route = RouteOf(httpContext);
        var trace = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        var pg = FindPostgres(exception);
        if (pg is not null && DatabaseCodes.TryGetValue(pg.SqlState, out var known))
        {
            writer.TryEnqueue(new ErrorEventInput(
                ErrorLayer.Database, known.Severity, known.Category, $"sqlstate {pg.SqlState} on {route}", trace));
            return;
        }

        var (status, _, _) = ApiExceptionHandler.Resolve(ApiErrorScope.Entered(httpContext), exception);
        if (status >= 500)
        {
            writer.TryEnqueue(new ErrorEventInput(
                ErrorLayer.Api, ErrorSeverity.Critical, "api.unhandled_exception",
                $"{exception.GetType().Name} on {route}", trace));
        }
    }

    private static PostgresException? FindPostgres(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException pg)
                return pg;
        }

        return null;
    }

    internal static string RouteOf(HttpContext context) =>
        (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
}

/// <summary>Reports a request that took longer than the threshold; the route pattern is used, never the raw path.</summary>
public sealed class SlowRequestMiddleware(RequestDelegate next, ErrorEventWriter? writer = null, SlowRequestOptions? options = null)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started);
            var threshold = (options ?? SlowRequestOptions.Default).Threshold;
            if (writer is not null && elapsed > threshold)
            {
                var severity = elapsed > threshold * 3 ? ErrorSeverity.Error : ErrorSeverity.Warning;
                writer.TryEnqueue(new ErrorEventInput(
                    ErrorLayer.Api, severity, "api.slow_request",
                    $"{context.Request.Method} {ServerErrorObserver.RouteOf(context)} {(int)elapsed.TotalMilliseconds} ms",
                    Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier));
            }
        }
    }
}

/// <summary>Hands the same changes to several sinks; one failing sink never starves the others.</summary>
public sealed class CompositeErrorEventSink(params IErrorEventChangeSink?[] sinks) : IErrorEventChangeSink
{
    public async Task OnChangesAsync(IReadOnlyList<ErrorEventChange> changes, CancellationToken cancellationToken)
    {
        List<Exception>? failures = null;
        foreach (var sink in sinks.OfType<IErrorEventChangeSink>())
        {
            try
            {
                await sink.OnChangesAsync(changes, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                (failures ??= []).Add(exception);
            }
        }

        if (failures is not null)
            throw new AggregateException(failures);
    }
}

public sealed class ErrorEventWriterHostedService(ErrorEventWriter writer) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        writer.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => writer.FlushAsync(cancellationToken);
}

/// <summary>Deletes expired monitoring rows once a day and writes one audit event per run.</summary>
public sealed class ErrorRetentionHostedService(
    MonitoringDataSource monitoring, IAuditEventStore audit, TimeSpan? interval = null, ErrorEventWriter? writer = null) : BackgroundService
{
    public static readonly IReadOnlyList<TelemetryRetentionPolicy> Policies =
    [
        new("observability.error_occurrences", "occurred_at", TelemetryRetention.RawEventDays),
        new("observability.raw_ui_interactions", "occurred_at", TelemetryRetention.RawEventDays),
        new("observability.error_events", "last_seen_at", TelemetryRetention.ErrorEventDays),
    ];

    public async Task RunOnceAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var removed = await TelemetryRetention.PurgeAsync(monitoring.Inner, Policies, now, cancellationToken: cancellationToken);
        await audit.AppendAsync(
            new AuditEvent(
                Guid.NewGuid(), "monitoring.retention.run", "MonitoringRetention", Guid.NewGuid(), "System",
                "monitoring-retention", reason: "Scheduled monitoring data purge",
                afterStateJson: JsonSerializer.Serialize(removed)),
            cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var wait = interval ?? TimeSpan.FromHours(24);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                writer?.TryEnqueue(new ErrorEventInput(
                    ErrorLayer.Background, ErrorSeverity.Error, "monitoring.retention_failed", exception.GetType().Name));
            }

            await Task.Delay(wait, stoppingToken);
        }
    }
}

public static class ServerDetectorsExtensions
{
    public static IServiceCollection AddServerErrorDetection(this IServiceCollection services, SlowRequestOptions? slow = null)
    {
        services.AddSingleton(slow ?? SlowRequestOptions.Default);
        services.AddSingleton(sp => new ErrorEventWriter(
            sp.GetRequiredService<MonitoringDataSource>().Inner,
            sink: new CompositeErrorEventSink(
                new AlertErrorEventSink(sp.GetRequiredService<IAlertService>()),
                sp.GetService<ALKAROS.Observability.ErrorNotifications.NotificationEnqueuer>())));
        services.AddHostedService<ErrorEventWriterHostedService>();
        services.AddHostedService(sp => new ErrorRetentionHostedService(
            sp.GetRequiredService<MonitoringDataSource>(), sp.GetRequiredService<IAuditEventStore>(),
            writer: sp.GetRequiredService<ErrorEventWriter>()));
        // The observer must run before the shared API error handler, which answers the request and ends the chain.
        services.Insert(0, ServiceDescriptor.Singleton<IExceptionHandler, ServerErrorObserver>());
        return services;
    }

    public static IApplicationBuilder UseMonitoringRequestTiming(this IApplicationBuilder application) =>
        application.UseMiddleware<SlowRequestMiddleware>();
}
