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

    public static IServiceCollection AddEndOfDayExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<System.Data.Common.DbDataSource>(
            serviceProvider => serviceProvider.GetRequiredService<NpgsqlDataSource>());
        services.TryAddScoped<IOperationalReportRepository, PostgresOperationalReportRepository>();
        services.TryAddScoped<IOperationalReportService, OperationalReportService>();
        services.TryAddScoped<IBusinessDayTotalsReader, PostgresBusinessDayTotalsReader>();

        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        services.TryAddScoped<EndOfDayAuthentication>();
        return services;
    }

    public static RouteGroupBuilder MapEndOfDayApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
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
            CloseBusinessDayV1 request,
            IOperationalReportService reports,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = EndOfDayEndpointFilter.RequireActorId(context);
            await authorization.AuthorizeAsync(actorId, ClosePermission, cancellationToken);
            var result = await reports.CloseBusinessDayAsync(
                businessDate,
                DateTimeOffset.UtcNow,
                request.CancelledItems,
                request.PrintFailures,
                request.WaiterSummaries?.Select(w => w.ToDomain(businessDate)).ToArray(),
                request.PrintSummaries?.Select(p => p.ToDomain(businessDate)).ToArray(),
                cancellationToken);
            return Results.Ok(BusinessDayReportV1.From(result));
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

public sealed record WaiterPerformanceV1(
    Guid WaiterUserId, int OrdersServedCount, decimal TotalSalesAmount, int CancellationsCount, decimal DiscountsAppliedAmount)
{
    public WaiterPerformanceRecord ToDomain(DateOnly businessDate)
        => new(Guid.NewGuid(), businessDate, WaiterUserId, OrdersServedCount, TotalSalesAmount,
            CancellationsCount, DiscountsAppliedAmount, DateTimeOffset.UtcNow);
}

public sealed record PrintErrorSummaryV1(
    string StationName, int TotalPrintJobs, int FailedPrintJobs, int RecoveredPrintJobs)
{
    public PrintErrorSummaryRecord ToDomain(DateOnly businessDate)
        => new(Guid.NewGuid(), businessDate, StationName, TotalPrintJobs, FailedPrintJobs, RecoveredPrintJobs, DateTimeOffset.UtcNow);
}

/// <summary>
/// V1-RMD-421 (V1-RMD-393 F-10): revenue and order count are no longer part of the request; the server reads them
/// from the recorded payments and orders of the business-day window.
/// </summary>
public sealed record CloseBusinessDayV1(
    int CancelledItems,
    int PrintFailures,
    IReadOnlyList<WaiterPerformanceV1>? WaiterSummaries = null,
    IReadOnlyList<PrintErrorSummaryV1>? PrintSummaries = null);

public sealed record BusinessDayV1(
    Guid BusinessDayId, DateOnly BusinessDate, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt,
    string Status, decimal TotalRevenue, int TotalOrdersCount, int TotalCancelledItemsCount, int TotalPrintFailuresCount)
{
    public static BusinessDayV1 From(BusinessDayRecord record)
        => new(record.BusinessDayId, record.BusinessDate, record.OpenedAt, record.ClosedAt, record.Status.ToString(),
            record.TotalRevenue, record.TotalOrdersCount, record.TotalCancelledItemsCount, record.TotalPrintFailuresCount);
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
        try
        {
            var actorId = await _authentication.AuthenticateAsync(context.HttpContext, context.HttpContext.RequestAborted);
            await _authorization.AuthorizeAsync(actorId, EndOfDayEndpoints.ViewPermission, context.HttpContext.RequestAborted);
            context.HttpContext.Items[ActorIdItemKey] = actorId;
            return await next(context);
        }
        catch (EndOfDayUnauthorizedException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (AuthorizationDeniedException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (ReportingException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (PostgresException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (NpgsqlException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (ArgumentException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (BadHttpRequestException exception)
        {
            return MapError(context.HttpContext, exception);
        }
    }

    private static IResult MapError(HttpContext context, Exception exception)
    {
        var (status, code, message) = exception switch
        {
            EndOfDayUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Gün sonu raporu/işlemi için yeterli izin yok."),
            BusinessDayNotFoundException =>
                (StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen iş günü bulunamadı."),
            BusinessDayAlreadyOpenException =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Bu tarih için iş günü zaten açık."),
            InvalidBusinessDayOperationException =>
                (StatusCodes.Status409Conflict, "INVALID_OPERATION", "Bu işlem şu anki gün durumuyla uyumlu değil."),
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } =>
                (StatusCodes.Status409Conflict, "DUPLICATE_RESOURCE", "Aynı kimlikte bir kayıt zaten var."),
            ArgumentException or BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => throw exception,
        };
        return Results.Json(
            new EndOfDayApiErrorEnvelopeV1(new EndOfDayApiErrorV1(code, message, status, context.TraceIdentifier)),
            statusCode: status);
    }
}

public sealed record EndOfDayApiErrorV1(string Code, string Message, int Status, string TraceId);

public sealed record EndOfDayApiErrorEnvelopeV1(EndOfDayApiErrorV1 Error);

public sealed class EndOfDayUnauthorizedException : Exception
{
    public EndOfDayUnauthorizedException() : base("A valid EOD reporting session is required.")
    {
    }
}
