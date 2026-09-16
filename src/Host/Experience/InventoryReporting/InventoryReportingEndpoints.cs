using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Reporting.MenuInventory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data.Common;

namespace ALKAROS.Host.Experience.InventoryReporting;

/// <summary>
/// V11-RPT-002: <c>IMenuInventoryReportingService.GetCriticalStockReportAsync</c>
/// has existed since V1.1 but no Host endpoint ever called it — greenfield.
/// Gated on <c>reports.view</c> (the same permission
/// <c>AuthorizationDecisionEndpoints</c>/Kitchen's own performance report
/// use), not <c>inventory.manage</c> — reading a report is a different
/// concern from configuring stock, and reports.view is already the
/// established gate for this kind of read.
/// </summary>
public static class InventoryReportingEndpoints
{
    public const string ManagerCookieName = "alkaros.manager";
    public const string ViewPermission = ApplicationPermissions.ReportsView;

    public static IServiceCollection AddInventoryReportingExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DbDataSource>(serviceProvider =>
            serviceProvider.GetRequiredService<NpgsqlDataSource>());
        services.TryAddScoped<IMenuInventoryReportingService, PostgresMenuInventoryReportingService>();

        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        services.TryAddScoped<InventoryReportingAuthentication>();

        // AddSignalR is idempotent to call more than once (see
        // HelpRequestExperience's own note on this).
        services.AddSignalR(options => options.EnableDetailedErrors = false);
        services.AddHostedService<LowStockAlertHostedService>();
        return services;
    }

    public static RouteGroupBuilder MapInventoryReportingApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapHub<LowStockAlertHub>(LowStockAlertHub.Route);

        var group = endpoints.MapGroup("/api/v1/management/inventory/reports");
        group.AddEndpointFilter<InventoryReportingEndpointFilter>();

        group.MapGet("/critical-stock", async (
            Guid? locationId,
            decimal? criticalThreshold,
            IMenuInventoryReportingService service,
            CancellationToken cancellationToken) =>
        {
            var report = await service.GetCriticalStockReportAsync(
                new CriticalStockReportQuery(locationId, criticalThreshold), cancellationToken);
            return Results.Ok(report);
        });

        return group;
    }
}

public sealed class InventoryReportingAuthentication
{
    private readonly NpgsqlDataSource _dataSource;

    public InventoryReportingAuthentication(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Guid> AuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rawToken = context.Request.Cookies[InventoryReportingEndpoints.ManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(_dataSource, rawToken, allowSupervisor: true, cancellationToken);
        return actorId ?? throw new InventoryReportingUnauthorizedException();
    }
}

public sealed class InventoryReportingEndpointFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5800, nameof(LogRequestFailure)),
            "Inventory reporting request failed on {Path} ({TraceIdentifier}).");

    private readonly InventoryReportingAuthentication _authentication;
    private readonly IAuthorizationService _authorization;
    private readonly ILogger<InventoryReportingEndpointFilter> _logger;

    public InventoryReportingEndpointFilter(
        InventoryReportingAuthentication authentication, IAuthorizationService authorization, ILogger<InventoryReportingEndpointFilter> logger)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            var actorId = await _authentication.AuthenticateAsync(context.HttpContext, context.HttpContext.RequestAborted);
            await _authorization.AuthorizeAsync(actorId, InventoryReportingEndpoints.ViewPermission, context.HttpContext.RequestAborted);
            return await next(context);
        }
        catch (Exception exception)
        {
            var mapped = Map(exception);
            if (mapped.Status >= StatusCodes.Status500InternalServerError)
            {
                LogRequestFailure(_logger, context.HttpContext.Request.Path, context.HttpContext.TraceIdentifier, exception);
            }

            return Results.Json(
                new { error = new { code = mapped.Code, message = mapped.Message } },
                statusCode: mapped.Status);
        }
    }

    private static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        InventoryReportingUnauthorizedException => (401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
        AuthorizationDeniedException => (403, "FORBIDDEN", "Rapor görüntüleme izni gerekiyor."),
        ArgumentException or BadHttpRequestException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        PostgresException or NpgsqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };
}

public sealed class InventoryReportingUnauthorizedException : Exception
{
    public InventoryReportingUnauthorizedException() : base("A valid manager/supervisor session is required.")
    {
    }
}
