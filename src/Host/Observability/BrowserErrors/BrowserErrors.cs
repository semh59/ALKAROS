using System.Text.RegularExpressions;
using ALKAROS.Host.DualScreen;
using ALKAROS.Observability.ErrorEvents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ALKAROS.Host.Observability.BrowserErrors;

/// <summary>What a browser may tell the server: an id, a fixed kind and a route; free text never travels.</summary>
public sealed record BrowserErrorReport(string? EventId, string? Kind, string? Route);

public enum BrowserErrorVerdict { Accepted, Duplicate, RateLimited, Invalid }

/// <summary>
/// Accepts browser error reports: kinds map to a fixed catalog (anything else becomes "browser.unknown"), the
/// route is reduced to a template, repeats of one event id are dropped, and each session has its own quota so a
/// noisy page cannot crowd out real server-side errors.
/// </summary>
public sealed partial class BrowserErrorIngest(ErrorEventWriter writer, Func<DateTimeOffset>? clock = null)
{
    public const int PerSessionPerMinute = 30;
    public const int SeenCapacity = 5000;

    private static readonly Dictionary<string, (string Category, ErrorSeverity Severity)> Catalog =
        new Dictionary<string, (string, ErrorSeverity)>(StringComparer.Ordinal)
        {
            ["js_error"] = ("browser.js_error", ErrorSeverity.Error),
            ["unhandled_rejection"] = ("browser.unhandled_rejection", ErrorSeverity.Error),
            ["network_error"] = ("browser.network_error", ErrorSeverity.Warning),
            ["render_error"] = ("browser.render_error", ErrorSeverity.Error),
        };

    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly object _gate = new();
    private readonly Queue<string> _seenOrder = new();
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, Queue<DateTimeOffset>> _quota = [];

    public BrowserErrorVerdict Accept(BrowserErrorReport report, Guid sessionId, Guid terminalId)
    {
        if (string.IsNullOrWhiteSpace(report.EventId) || report.EventId.Length > 64)
            return BrowserErrorVerdict.Invalid;

        lock (_gate)
        {
            var now = _clock();
            if (!_quota.TryGetValue(sessionId, out var hits))
                _quota[sessionId] = hits = new Queue<DateTimeOffset>();
            while (hits.Count > 0 && now - hits.Peek() > TimeSpan.FromMinutes(1))
                hits.Dequeue();
            if (hits.Count >= PerSessionPerMinute)
                return BrowserErrorVerdict.RateLimited;
            if (!_seen.Add(report.EventId))
                return BrowserErrorVerdict.Duplicate;

            hits.Enqueue(now);
            _seenOrder.Enqueue(report.EventId);
            while (_seenOrder.Count > SeenCapacity)
                _seen.Remove(_seenOrder.Dequeue());
        }

        var (category, severity) = report.Kind is not null && Catalog.TryGetValue(report.Kind, out var known)
            ? known
            : ("browser.unknown", ErrorSeverity.Warning);
        writer.TryEnqueue(new ErrorEventInput(
            ErrorLayer.Browser, severity, category, RouteTemplate(report.Route),
            TerminalLabel: $"terminal:{terminalId.ToString("N")[..8]}"));
        return BrowserErrorVerdict.Accepted;
    }

    public static string RouteTemplate(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return "unknown";
        var path = route.Split('?', '#')[0];
        path = Guid_().Replace(path, ":id");
        path = Digits().Replace(path, ":n");
        return path.Length > 80 ? path[..80] : path;
    }

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex Guid_();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();
}

public static class BrowserErrorEndpoints
{
    public static IEndpointRouteBuilder MapBrowserErrors(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/telemetry/browser-errors", async (
            BrowserErrorReport report,
            HttpContext context,
            DualScreenStore store,
            BrowserErrorIngest ingest,
            CancellationToken cancellationToken) =>
        {
            var principal = await store.AuthenticateCashierByCookieAsync(
                context.Request.Cookies[DualScreenApplication.CashierCookieName], cancellationToken);
            if (principal is null)
                return Results.StatusCode(StatusCodes.Status401Unauthorized);

            return ingest.Accept(report, principal.SessionId, principal.TerminalId) switch
            {
                BrowserErrorVerdict.RateLimited => Results.StatusCode(StatusCodes.Status429TooManyRequests),
                BrowserErrorVerdict.Invalid => Results.StatusCode(StatusCodes.Status400BadRequest),
                _ => Results.StatusCode(StatusCodes.Status204NoContent),
            };
        });
        return app;
    }
}
