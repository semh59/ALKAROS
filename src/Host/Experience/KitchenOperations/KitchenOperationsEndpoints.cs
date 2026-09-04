using ALKAROS.Audit.EventStore;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Kitchen.PhysicalPrintRecovery;
using ALKAROS.Kitchen.PrintQueue;
using ALKAROS.Kitchen.Routing;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Messaging;
using ALKAROS.Operations.BackupHealth;
using ALKAROS.Settings.TypedSettings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data.Common;

namespace ALKAROS.Host.Experience.KitchenOperations;

public static class KitchenOperationsEndpoints
{
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/kitchen-operations";
    public const string TicketMutationPermission = ApplicationPermissions.OrdersSend;
    public const string RoutingMutationPermission = "kitchen.routing.manage";
    public const string ReprintPermission = "kitchen.reprint";
    public const string BackupPermission = "operations.backup";

    public static IServiceCollection AddKitchenOperationsExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DbDataSource>(serviceProvider =>
            serviceProvider.GetRequiredService<NpgsqlDataSource>());
        services.TryAddSingleton<DualScreenStore>();
        services.TryAddSingleton<IRoleRepository, PostgresRoleRepository>();
        services.TryAddSingleton<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddSingleton<IAuthorizationService, AuthorizationService>();
        services.TryAddSingleton<IKitchenTicketRepository, PostgresKitchenTicketRepository>();
        services.TryAddSingleton<IPrinterRepository, PostgresPrinterRepository>();
        services.TryAddSingleton<IPrinterRouteRepository, PostgresPrinterRouteRepository>();
        services.TryAddSingleton<IPrintQueueRepository, PostgresPrintQueueRepository>();
        services.TryAddSingleton<IPhysicalPrintRecoveryRepository, PostgresPhysicalPrintRecoveryRepository>();
        services.TryAddSingleton<IBackupHealthRepository, PostgresBackupHealthRepository>();
        services.TryAddSingleton<IBackupEngine, LocalBackupEngine>();
        services.TryAddSingleton<IBackupHealthService, BackupHealthService>();
        services.TryAddSingleton<IAuditEventStore, PostgresAuditEventStore>();
        // V1-KIT-005: KitchenOperationsStore publishes item state changes for
        // Orders to mirror, gated by kitchen.live_sync_enabled.
        services.TryAddSingleton<ISettingsRepository, PostgresSettingsRepository>();
        services.TryAddSingleton<ISettingValidator, SettingValidator>();
        services.TryAddSingleton<ISettingsService, SettingsService>();
        services.TryAddSingleton(sp => new OutboxStore(sp.GetRequiredService<NpgsqlDataSource>()));
        services.TryAddSingleton<KitchenOperationsStore>();
        services.TryAddSingleton<IKitchenOperationsSessionAuthorizer, KitchenOperationsSessionAuthorizer>();
        services.TryAddSingleton(_ => new ProductionBackupOptions(
            Environment.GetEnvironmentVariable("ALKAROS_BACKUP_DIRECTORY")));
        services.TryAddTransient<KitchenOperationsExceptionFilter>();
        return services;
    }

    public static RouteGroupBuilder MapKitchenOperationsApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("KitchenOperations")
            .AddEndpointFilter<KitchenOperationsExceptionFilter>();

        group.MapGet("/tickets", async (
            Guid terminalId,
            string? stationId,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.GetActiveTicketsAsync(stationId ?? string.Empty, cancellationToken));
        });

        group.MapGet("/tickets/{ticketId:guid}", async (
            Guid terminalId,
            Guid ticketId,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.GetTicketAsync(ticketId, cancellationToken));
        });

        group.MapPost("/tickets/{ticketId:guid}/transition", async (
            Guid terminalId,
            Guid ticketId,
            TransitionKitchenTicketV1 request,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequirePermissionAsync(
                context, terminalId, TicketMutationPermission, cancellationToken);
            return Results.Ok(await store.TransitionTicketAsync(ticketId, request, cancellationToken));
        });

        group.MapPost("/tickets/{ticketId:guid}/items/{itemId:guid}/transition", async (
            Guid terminalId,
            Guid ticketId,
            Guid itemId,
            TransitionKitchenItemV1 request,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequirePermissionAsync(
                context, terminalId, TicketMutationPermission, cancellationToken);
            return Results.Ok(await store.TransitionItemAsync(ticketId, itemId, request, cancellationToken));
        });

        group.MapGet("/printers", async (
            Guid terminalId,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.GetPrintersAsync(cancellationToken));
        });

        group.MapPut("/printers/{printerId:guid}", async (
            Guid terminalId,
            Guid printerId,
            UpdatePrinterV1 request,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequirePermissionAsync(
                context, terminalId, RoutingMutationPermission, cancellationToken);
            return Results.Ok(await store.UpdatePrinterAsync(printerId, request, cancellationToken));
        });

        group.MapGet("/routes", async (
            Guid terminalId,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.GetRoutesAsync(cancellationToken));
        });

        group.MapPut("/routes/{routeId:guid}", async (
            Guid terminalId,
            Guid routeId,
            UpdatePrinterRouteV1 request,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequirePermissionAsync(
                context, terminalId, RoutingMutationPermission, cancellationToken);
            return Results.Ok(await store.UpdateRouteAsync(routeId, request, cancellationToken));
        });

        group.MapGet("/print-jobs", async (
            Guid terminalId,
            Guid? ticketId,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            if (ticketId is null || ticketId == Guid.Empty)
                throw new ArgumentException("ticketId is required.", nameof(ticketId));
            return Results.Ok(await store.GetPrintJobsAsync(ticketId.Value, cancellationToken));
        });

        group.MapGet("/print-jobs/{jobId:guid}", async (
            Guid terminalId,
            Guid jobId,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.GetPrintJobAsync(jobId, cancellationToken));
        });

        group.MapGet("/deliveries/unknown", async (
            Guid terminalId,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.GetUnknownDeliveriesAsync(cancellationToken));
        });

        group.MapGet("/deliveries/{deliveryId:guid}", async (
            Guid terminalId,
            Guid deliveryId,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.GetDeliveryAsync(deliveryId, cancellationToken));
        });

        group.MapPost("/deliveries/{deliveryId:guid}/reprint-approval", async (
            Guid terminalId,
            Guid deliveryId,
            ReprintDecisionV1 request,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequirePermissionAsync(
                context, terminalId, ReprintPermission, cancellationToken);
            return Results.Ok(await store.ApproveReprintAsync(
                deliveryId, request, principal.UserId, cancellationToken));
        });

        group.MapPost("/deliveries/{deliveryId:guid}/reprint-rejection", async (
            Guid terminalId,
            Guid deliveryId,
            ReprintDecisionV1 request,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequirePermissionAsync(
                context, terminalId, ReprintPermission, cancellationToken);
            return Results.Ok(await store.RejectReprintAsync(
                deliveryId, request, principal.UserId, cancellationToken));
        });

        group.MapGet("/operations/health/latest", async (
            Guid terminalId,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            var latest = await store.GetLatestHealthAsync(cancellationToken);
            return latest is null ? Results.NotFound() : Results.Ok(latest);
        });

        group.MapGet("/operations/backups/recent", async (
            Guid terminalId,
            int? limit,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.GetRecentBackupsAsync(limit ?? 10, cancellationToken));
        });

        group.MapPost("/operations/backups", async (
            Guid terminalId,
            StartBackupV1 request,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            ProductionBackupOptions options,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequirePermissionAsync(
                context, terminalId, BackupPermission, cancellationToken);
            var provider = context.RequestServices.GetService<IProductionBackupPayloadProvider>();
            return Results.Ok(await store.ExecuteBackupAsync(request, options, provider, cancellationToken));
        });

        group.MapGet("/audit/aggregate/{aggregateType}/{aggregateId:guid}", async (
            Guid terminalId,
            string aggregateType,
            Guid aggregateId,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.GetAuditByAggregateAsync(aggregateType, aggregateId, cancellationToken));
        });

        group.MapGet("/audit/correlation/{correlationId}", async (
            Guid terminalId,
            string correlationId,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.GetAuditByCorrelationAsync(correlationId, cancellationToken));
        });

        return group;
    }
}

public sealed record KitchenOperationsPrincipal(Guid UserId, Guid TerminalId);

public interface IKitchenOperationsSessionAuthorizer
{
    Task<KitchenOperationsPrincipal> RequireReadAsync(
        HttpContext context,
        Guid terminalId,
        CancellationToken cancellationToken);

    Task<KitchenOperationsPrincipal> RequirePermissionAsync(
        HttpContext context,
        Guid terminalId,
        string permission,
        CancellationToken cancellationToken);
}

internal sealed class KitchenOperationsSessionAuthorizer : IKitchenOperationsSessionAuthorizer
{
    private readonly DualScreenStore _sessions;
    private readonly IAuthorizationService _authorization;

    public KitchenOperationsSessionAuthorizer(
        DualScreenStore sessions,
        IAuthorizationService authorization)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
    }

    public async Task<KitchenOperationsPrincipal> RequireReadAsync(
        HttpContext context,
        Guid terminalId,
        CancellationToken cancellationToken)
    {
        if (terminalId == Guid.Empty)
            throw new ArgumentException("Terminal ID cannot be empty.", nameof(terminalId));
        var cashier = await _sessions.AuthenticateCashierAsync(
            context.Request.Cookies[DualScreenApplication.CashierCookieName],
            terminalId,
            cancellationToken);
        if (cashier is not null)
            return new KitchenOperationsPrincipal(cashier.UserId, terminalId);

        if (await _sessions.AuthenticateDisplayAsync(
                context.Request.Cookies[DualScreenApplication.DisplayCookieName],
                null,
                cancellationToken) is not null)
        {
            throw new KitchenOperationsForbiddenException("Customer displays cannot access kitchen operations.");
        }

        throw new KitchenOperationsUnauthorizedException("A valid terminal-bound cashier session is required.");
    }

    public async Task<KitchenOperationsPrincipal> RequirePermissionAsync(
        HttpContext context,
        Guid terminalId,
        string permission,
        CancellationToken cancellationToken)
    {
        var principal = await RequireReadAsync(context, terminalId, cancellationToken);
        await _authorization.AuthorizeAsync(principal.UserId, permission, cancellationToken);
        return principal;
    }
}

public sealed class KitchenOperationsExceptionFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5200, nameof(LogRequestFailure)),
            "Kitchen operations request failed on {Path} ({TraceIdentifier}).");

    private readonly ILogger<KitchenOperationsExceptionFilter> _logger;

    public KitchenOperationsExceptionFilter(ILogger<KitchenOperationsExceptionFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception exception)
        {
            var mapped = Map(exception);
            if (mapped.Status >= 500)
            {
                LogRequestFailure(
                    _logger,
                    context.HttpContext.Request.Path,
                    context.HttpContext.TraceIdentifier,
                    exception);
            }

            return Results.Json(
                new KitchenOperationsErrorEnvelopeV1(
                    new KitchenOperationsErrorV1(
                        mapped.Code,
                        mapped.Message,
                        mapped.Status,
                        context.HttpContext.TraceIdentifier)),
                statusCode: mapped.Status);
        }
    }

    private static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        KitchenOperationsUnauthorizedException => (401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
        KitchenOperationsForbiddenException or AuthorizationDeniedException =>
            (403, "FORBIDDEN", "Bu işlem için yetkiniz yok."),
        KitchenOperationsNotFoundException
            or KitchenTicketNotFoundException => (404, "NOT_FOUND", "İstenen mutfak kaydı bulunamadı."),
        KitchenOperationsConcurrencyException
            or StaleKitchenTicketVersionException
            or PrintJobConcurrencyException
            or PhysicalPrintDeliveryConcurrencyException =>
            (409, "CONCURRENT_MODIFICATION", "Kayıt başka bir işlem tarafından değiştirildi."),
        InvalidKitchenTransitionException
            or InvalidPrintJobTransitionException
            or InvalidPhysicalPrintTransitionException
            or UnauthorizedReprintException
            or InvalidPrinterConfigurationException =>
            (409, "DOMAIN_CONFLICT", "İşlem mevcut durumla çakışıyor."),
        BackupProviderUnavailableException =>
            (503, "BACKUP_NOT_CONFIGURED", "Doğrulanmış production yedek sağlayıcısı yapılandırılmamış."),
        BackupExecutionException =>
            (503, "BACKUP_FAILED", "Yedekleme başarısız oldu; başarı olarak raporlanmadı."),
        PostgresException postgres when postgres.SqlState == PostgresErrorCodes.UniqueViolation =>
            (409, "DUPLICATE_RESOURCE", "Aynı kaynak zaten mevcut."),
        PostgresException postgres when postgres.SqlState == PostgresErrorCodes.ForeignKeyViolation =>
            (409, "RESOURCE_IN_USE", "Bağlı kayıtlar nedeniyle işlem tamamlanamadı."),
        ArgumentException or BadHttpRequestException =>
            (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        NpgsqlException =>
            (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };
}
