using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Host.Composition.Errors;
using ALKAROS.Host.Experience.Observability;
using ALKAROS.Host.Observability.Wiring;
using ALKAROS.Identity.Authorization;
using ALKAROS.Observability.PerformanceMetrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ALKAROS.Host.Observability.PerformanceBudgetEndpoints;

public sealed record PerformanceBudgetItem(string Metric, string Name, double P95Ms);

public sealed record PerformanceBudgetList(double DefaultRequestP95Ms, IReadOnlyList<PerformanceBudgetItem> Items);

public sealed class PerformanceBudgetQueries(NpgsqlDataSource dataSource, double defaultRequestP95Ms)
{
    public const int MaxNameLength = 200;
    public const double MaxP95Ms = 3_600_000;

    private static readonly HashSet<string> Metrics = new(StringComparer.Ordinal)
        { PerformanceMetric.Request, PerformanceMetric.DbCommand, PerformanceMetric.QueueDepth, PerformanceMetric.UiInteraction };

    public static bool IsValid(PerformanceBudgetItem item) =>
        Metrics.Contains(item.Metric)
        && !string.IsNullOrWhiteSpace(item.Name) && item.Name.Length <= MaxNameLength
        && double.IsFinite(item.P95Ms) && item.P95Ms > 0 && item.P95Ms <= MaxP95Ms;

    public async Task<PerformanceBudgetList> ListAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT metric, name, p95_ms FROM observability.perf_budgets ORDER BY metric, name LIMIT 1000;");
        var items = new List<PerformanceBudgetItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            items.Add(new PerformanceBudgetItem(reader.GetString(0), reader.GetString(1), reader.GetDouble(2)));
        return new PerformanceBudgetList(defaultRequestP95Ms, items);
    }

    public async Task SetAsync(PerformanceBudgetItem item, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            INSERT INTO observability.perf_budgets (metric, name, p95_ms) VALUES (@metric, @name, @p95)
            ON CONFLICT (metric, name) DO UPDATE SET p95_ms = EXCLUDED.p95_ms;
            """);
        command.Parameters.AddWithValue("metric", item.Metric);
        command.Parameters.AddWithValue("name", item.Name);
        command.Parameters.AddWithValue("p95", item.P95Ms);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RemoveAsync(string metric, string name, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("DELETE FROM observability.perf_budgets WHERE metric = @metric AND name = @name;");
        command.Parameters.AddWithValue("metric", metric);
        command.Parameters.AddWithValue("name", name);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }
}

public static class PerformanceBudgetEndpointsExtensions
{
    public static IServiceCollection AddPerformanceBudgetEndpoints(this IServiceCollection services, double defaultRequestP95Ms)
    {
        services.AddSingleton(sp => new PerformanceBudgetQueries(sp.GetRequiredService<MonitoringDataSource>().Inner, defaultRequestP95Ms));
        return services;
    }

    public static IEndpointRouteBuilder MapPerformanceBudgetEndpoints(this IEndpointRouteBuilder app)
    {
        ApiErrorHandling.EnsureFor(app);
        var group = app.MapGroup("/api/v1/management/observability/performance-budgets");
        group.AddEndpointFilter<ObservabilityEndpointFilter>();

        group.MapGet("/", async (PerformanceBudgetQueries queries, CancellationToken cancellationToken) =>
            Results.Ok(await queries.ListAsync(cancellationToken)));

        group.MapPut("/", async (
            PerformanceBudgetItem item, PerformanceBudgetQueries queries, [FromServices] IAuthorizationService authorization,
            [FromServices] IAuditEventStore audit, HttpContext context, CancellationToken cancellationToken) =>
        {
            var actorId = await RequireManagerAsync(context, authorization, cancellationToken);
            if (!PerformanceBudgetQueries.IsValid(item))
                return Results.BadRequest();

            // The audit record is written first: the budget lives in another data source, so it cannot share a transaction.
            await AuditAsync(audit, "monitoring.performance_budget.set", actorId, item, cancellationToken);
            await queries.SetAsync(item, cancellationToken);
            return Results.NoContent();
        });

        group.MapDelete("/", async (
            string metric, string name, PerformanceBudgetQueries queries, [FromServices] IAuthorizationService authorization,
            [FromServices] IAuditEventStore audit, HttpContext context, CancellationToken cancellationToken) =>
        {
            var actorId = await RequireManagerAsync(context, authorization, cancellationToken);
            var existing = (await queries.ListAsync(cancellationToken)).Items.FirstOrDefault(
                budget => budget.Metric == metric && budget.Name == name);
            if (existing is null)
                return Results.NotFound();

            await AuditAsync(audit, "monitoring.performance_budget.removed", actorId, new PerformanceBudgetItem(metric, name, 0), cancellationToken);
            return await queries.RemoveAsync(metric, name, cancellationToken) ? Results.NoContent() : Results.NotFound();
        });
        return app;
    }

    private static async Task<Guid> RequireManagerAsync(HttpContext context, IAuthorizationService authorization, CancellationToken cancellationToken)
    {
        var actorId = ObservabilityEndpointFilter.RequireActorId(context);
        await authorization.AuthorizeAsync(actorId, ObservabilityEndpoints.ManagePermission, cancellationToken);
        return actorId;
    }

    private static Task AuditAsync(IAuditEventStore audit, string eventName, Guid actorId, PerformanceBudgetItem item, CancellationToken cancellationToken) =>
        audit.AppendAsync(
            new AuditEvent(
                Guid.NewGuid(), eventName, "PerformanceBudget", Guid.NewGuid(), "User", Guid.NewGuid().ToString("N"),
                actorId: actorId, reason: "Management performance budgets",
                afterStateJson: JsonSerializer.Serialize(item)),
            cancellationToken);
}
