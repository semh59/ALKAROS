using ALKAROS.Host.DualScreen;
using ALKAROS.Observability.ErrorEvents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ALKAROS.Host.Observability.OfflineQueueReport;

/// <summary>What a terminal may tell the server about its offline order queue: one count and nothing else.</summary>
public sealed record QueueLengthReport(int? Length);

public enum OfflineQueueVerdict { Accepted, Invalid }

/// <summary>
/// Turns the reported length of a terminal's offline queue into one event per terminal: at or above the limit the
/// event is raised (and re-opened when it comes back), below the limit it is resolved. The terminal label comes
/// from the session, never from the request.
/// </summary>
public sealed class OfflineQueueIngest(ErrorEventWriter writer, NpgsqlDataSource monitoring, int limit = OfflineQueueIngest.DefaultLimit)
{
    public const int DefaultLimit = 20;
    public const int MaxPlausibleLength = 100_000;
    public const string Category = "browser.offline_queue_backlog";

    public async Task<OfflineQueueVerdict> AcceptAsync(QueueLengthReport report, Guid terminalId, CancellationToken cancellationToken)
    {
        if (report.Length is not { } length || length < 0 || length > MaxPlausibleLength)
            return OfflineQueueVerdict.Invalid;

        var label = $"terminal:{terminalId.ToString("N")[..8]}";
        var detail = $"offline order queue over its limit on {label}";
        if (length >= limit)
        {
            var severity = length >= limit * 5 ? ErrorSeverity.Error : ErrorSeverity.Warning;
            writer.TryEnqueue(new ErrorEventInput(ErrorLayer.Browser, severity, Category, detail, TerminalLabel: label));
            return OfflineQueueVerdict.Accepted;
        }

        await using var command = monitoring.CreateCommand(
            """
            UPDATE observability.error_events SET status = 'Resolved', resolved_at = now()
            WHERE fingerprint = @fingerprint AND status <> 'Resolved';
            """);
        command.Parameters.AddWithValue("fingerprint", ErrorFingerprint.Compute(ErrorLayer.Browser, Category, detail));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return OfflineQueueVerdict.Accepted;
    }
}

public static class OfflineQueueEndpoints
{
    public static IServiceCollection AddOfflineQueueReport(this IServiceCollection services, int? limit = null) =>
        services.AddSingleton(sp => new OfflineQueueIngest(
            sp.GetRequiredService<ErrorEventWriter>(),
            sp.GetRequiredService<ALKAROS.Host.Observability.Wiring.MonitoringDataSource>().Inner,
            limit ?? OfflineQueueIngest.DefaultLimit));

    public static IEndpointRouteBuilder MapOfflineQueueReport(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/telemetry/offline-queue", async (
            QueueLengthReport report,
            HttpContext context,
            DualScreenStore store,
            OfflineQueueIngest ingest,
            CancellationToken cancellationToken) =>
        {
            var principal = await store.AuthenticateCashierByCookieAsync(
                context.Request.Cookies[DualScreenApplication.CashierCookieName], cancellationToken);
            if (principal is null)
                return Results.StatusCode(StatusCodes.Status401Unauthorized);

            return await ingest.AcceptAsync(report, principal.TerminalId, cancellationToken) == OfflineQueueVerdict.Accepted
                ? Results.StatusCode(StatusCodes.Status204NoContent)
                : Results.StatusCode(StatusCodes.Status400BadRequest);
        });
        return app;
    }
}
