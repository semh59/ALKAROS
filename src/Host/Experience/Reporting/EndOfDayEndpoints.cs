using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Host.Composition.Errors;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Reporting.BusinessDayTotals;
using ALKAROS.Reporting.V1Operations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.Reporting;

/// <summary>
/// V1-RMD-249: found by an independent audit (2026-09-18) —
/// IOperationalReportService (V1-RPT-001: business-day open/close + EOD
/// report) was domain-complete, DI-registered, unit-tested, and had zero
/// HTTP surface. The route group's own filter only requires
/// <see cref="ViewPermission"/> (Supervisor+, same as
/// InventoryReportingEndpoints's own gate for reading a report); the two
/// mutating endpoints (open/close) additionally require
/// <see cref="ClosePermission"/> (manager-only) inside their own handler —
/// same "base filter authenticates, mutating handler escalates" shape as
/// CashSession's own supervisor-override check.
/// </summary>
public static class EndOfDayEndpoints
{
    public const string ManagerCookieName = "alkaros.manager";
    public const string ViewPermission = ApplicationPermissions.ReportsView;
    public const string ClosePermission = ApplicationPermissions.ReportsCloseDay;

    private static readonly JsonSerializerOptions AuditJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static IServiceCollection AddEndOfDayExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<System.Data.Common.DbDataSource>(
            serviceProvider => serviceProvider.GetRequiredService<NpgsqlDataSource>());
        services.TryAddScoped<IOperationalReportRepository, PostgresOperationalReportRepository>();
        services.TryAddScoped<IOperationalReportService, OperationalReportService>();
        services.TryAddScoped<IBusinessDayTotalsReader, PostgresBusinessDayTotalsReader>();
        services.TryAddSingleton<IAuditEventStore, PostgresAuditEventStore>();

        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        services.TryAddScoped<EndOfDayAuthentication>();
        return services;
    }

    public static RouteGroupBuilder MapEndOfDayApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ApiErrorHandling.EnsureFor(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/reporting/business-day");
        group.AddEndpointFilter<EndOfDayEndpointFilter>();

        group.MapPost("/open", async (
            OpenBusinessDayV1 request,
            IOperationalReportService reports,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = EndOfDayEndpointFilter.RequireActorId(context);
            await authorization.AuthorizeAsync(actorId, ClosePermission, cancellationToken);
            var day = await reports.OpenBusinessDayAsync(request.BusinessDate, DateTimeOffset.UtcNow, cancellationToken);
            return Results.Created(
                $"/api/v1/management/reporting/business-day/{day.BusinessDate:O}", BusinessDayV1.From(day));
        });

        group.MapPost("/{businessDate}/close", async (
            DateOnly businessDate,
            CloseBusinessDayV1? request,
            IOperationalReportService reports,
            IAuditEventStore audit,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = EndOfDayEndpointFilter.RequireActorId(context);
            await authorization.AuthorizeAsync(actorId, ClosePermission, cancellationToken);
            var result = await reports.CloseBusinessDayAsync(
                businessDate,
                DateTimeOffset.UtcNow,
                request?.AcknowledgeOpenItems ?? false,
                (day, figures, connection, transaction, token) => audit.AppendAsync(
                    new AuditEvent(
                        Guid.NewGuid(), "reporting.business_day_closed", "BusinessDay", day.BusinessDayId, "User",
                        day.BusinessDayId.ToString("N"), actorId,
                        afterStateJson: JsonSerializer.Serialize(new
                        {
                            businessDate = businessDate.ToString("O", System.Globalization.CultureInfo.InvariantCulture), revenue = figures.Revenue, tips = figures.Tips,
                            orders = figures.OrderCount, cancelledItems = figures.CancelledItems, printFailures = figures.PrintFailures,
                            openCashSessions = figures.OpenItems.CashSessions, openBills = figures.OpenItems.Bills,
                            unsettledPayments = figures.OpenItems.UnsettledPayments,
                            openItemsAcknowledged = day.OpenItemsAcknowledged,
                        }, AuditJson)),
                    (NpgsqlConnection)connection, (NpgsqlTransaction)transaction, token),
                cancellationToken);
            return Results.Ok(BusinessDayReportV1.From(result));
        });

        group.MapGet("/{businessDate}/close-preflight", async (
            DateOnly businessDate,
            IOperationalReportService reports,
            CancellationToken cancellationToken) =>
        {
            var day = await reports.GetBusinessDayByDateAsync(businessDate, cancellationToken)
                ?? throw new BusinessDayNotFoundException(businessDate);
            if (day.Status == BusinessDayStatus.Closed)
                throw new InvalidBusinessDayOperationException($"Business day '{businessDate:yyyy-MM-dd}' is already closed.");
            return Results.Ok(BusinessDayPreflightV1.From(businessDate, await reports.PreviewCloseAsync(businessDate, cancellationToken)));
        });

        group.MapGet("/{businessDate}", async (
            DateOnly businessDate,
            IOperationalReportService reports,
            CancellationToken cancellationToken) =>
        {
            var day = await reports.GetBusinessDayByDateAsync(businessDate, cancellationToken)
                ?? throw new BusinessDayNotFoundException(businessDate);
            return Results.Ok(BusinessDayV1.From(day));
        });

        group.MapGet("/{businessDate}/full-report", async (
            DateOnly businessDate,
            IOperationalReportService reports,
            CancellationToken cancellationToken) =>
        {
            var result = await reports.GetFullDailyReportAsync(businessDate, cancellationToken)
                ?? throw new BusinessDayNotFoundException(businessDate);
            return Results.Ok(BusinessDayReportV1.From(result));
        });

        return group;
    }
}

public sealed record OpenBusinessDayV1(DateOnly BusinessDate);

/// <summary>
/// The close takes no figures: the server counts everything itself. Only the manager's knowing answer to the open
/// items warning is sent.
/// </summary>
public sealed record CloseBusinessDayV1(bool AcknowledgeOpenItems = false);

/// <summary>What closing this business date would store and warn about right now.</summary>
public sealed record BusinessDayPreflightV1(
    DateOnly BusinessDate, decimal Revenue, decimal Tips, int Orders, int CancelledItems, int PrintFailures,
    int OpenCashSessions, int OpenBills, int UnsettledPayments, bool RequiresAcknowledgement)
{
    public static BusinessDayPreflightV1 From(DateOnly businessDate, BusinessDayFigures figures)
        => new(businessDate, figures.Revenue, figures.Tips, figures.OrderCount, figures.CancelledItems, figures.PrintFailures,
            figures.OpenItems.CashSessions, figures.OpenItems.Bills, figures.OpenItems.UnsettledPayments, figures.OpenItems.Any);
}

public sealed record BusinessDayV1(
    Guid BusinessDayId, DateOnly BusinessDate, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt,
    string Status, decimal TotalRevenue, int TotalOrdersCount, int TotalCancelledItemsCount, int TotalPrintFailuresCount,
    decimal TotalTipsAmount, int OpenCashSessionsCount, int OpenBillsCount, int UnsettledPaymentsCount,
    bool OpenItemsAcknowledged, IReadOnlyList<TableFigure> TableBreakdown, CashReconciliationFigure? CashReconciliation)
{
    public static BusinessDayV1 From(BusinessDayRecord record)
        => new(record.BusinessDayId, record.BusinessDate, record.OpenedAt, record.ClosedAt, record.Status.ToString(),
            record.TotalRevenue, record.TotalOrdersCount, record.TotalCancelledItemsCount, record.TotalPrintFailuresCount,
            record.TotalTipsAmount, record.OpenItemsAtClose?.CashSessions ?? 0, record.OpenItemsAtClose?.Bills ?? 0,
            record.OpenItemsAtClose?.UnsettledPayments ?? 0, record.OpenItemsAcknowledged, record.TableBreakdown ?? [],
            record.CashReconciliation);
}

public sealed record WaiterPerformanceResultV1(
    Guid WaiterUserId, int OrdersServedCount, decimal TotalSalesAmount, int CancellationsCount, decimal DiscountsAppliedAmount)
{
    public static WaiterPerformanceResultV1 From(WaiterPerformanceRecord record)
        => new(record.WaiterUserId, record.OrdersServedCount, record.TotalSalesAmount, record.CancellationsCount, record.DiscountsAppliedAmount);
}

public sealed record PrintErrorSummaryResultV1(
    string StationName, int TotalPrintJobs, int FailedPrintJobs, int RecoveredPrintJobs)
{
    public static PrintErrorSummaryResultV1 From(PrintErrorSummaryRecord record)
        => new(record.StationName, record.TotalPrintJobs, record.FailedPrintJobs, record.RecoveredPrintJobs);
}

public sealed record BusinessDayReportV1(
    BusinessDayV1 BusinessDay,
    IReadOnlyList<WaiterPerformanceResultV1> WaiterSummaries,
    IReadOnlyList<PrintErrorSummaryResultV1> PrintSummaries)
{
    public static BusinessDayReportV1 From(BusinessDayReportResult result)
        => new(BusinessDayV1.From(result.BusinessDay),
            result.WaiterSummaries.Select(WaiterPerformanceResultV1.From).ToArray(),
            result.PrintSummaries.Select(PrintErrorSummaryResultV1.From).ToArray());
}

public sealed class EndOfDayAuthentication
{
    private readonly NpgsqlDataSource _dataSource;

    public EndOfDayAuthentication(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Guid> AuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rawToken = context.Request.Cookies[EndOfDayEndpoints.ManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(_dataSource, rawToken, allowSupervisor: true, cancellationToken);
        return actorId ?? throw new EndOfDayUnauthorizedException();
    }
}

public sealed class EndOfDayEndpointFilter : IEndpointFilter
{
    private const string ActorIdItemKey = "EndOfDayActorId";

    private readonly EndOfDayAuthentication _authentication;
    private readonly IAuthorizationService _authorization;

    public EndOfDayEndpointFilter(EndOfDayAuthentication authentication, IAuthorizationService authorization)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
    }

    public static Guid RequireActorId(HttpContext context)
        => context.Items[ActorIdItemKey] as Guid?
            ?? throw new InvalidOperationException($"{nameof(EndOfDayEndpointFilter)} did not run before this endpoint.");

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ApiErrorScope.Enter(context.HttpContext, ApiErrorCatalog.EndOfDay);
        var actorId = await _authentication.AuthenticateAsync(context.HttpContext, context.HttpContext.RequestAborted);
        await _authorization.AuthorizeAsync(actorId, EndOfDayEndpoints.ViewPermission, context.HttpContext.RequestAborted);
        context.HttpContext.Items[ActorIdItemKey] = actorId;
        return await next(context);
    }

}

public sealed class EndOfDayUnauthorizedException : Exception
{
    public EndOfDayUnauthorizedException() : base("A valid EOD reporting session is required.")
    {
    }
}
