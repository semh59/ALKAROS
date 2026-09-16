using ALKAROS.Audit.EventStore;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.Catalog;
using ALKAROS.Host.Experience.WaiterNotifications;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Kitchen.PhysicalPrintRecovery;
using ALKAROS.Kitchen.PrintQueue;
using ALKAROS.Kitchen.Routing;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Messaging;
using ALKAROS.Operations.BackupHealth;
using ALKAROS.Orders.OrderAggregate;
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
    /// <summary>
    /// V1-IAM-028: kept for the "any mutation" case (unused directly by the
    /// transition endpoints anymore — see <see cref="TicketTransitionPermission"/> —
    /// but still the permission a Cancelled transition requires).
    /// </summary>
    public const string TicketMutationPermission = ApplicationPermissions.OrdersSend;
    public const string RoutingMutationPermission = "kitchen.routing.manage";
    public const string ReprintPermission = "kitchen.reprint";
    public const string BackupPermission = "operations.backup";
    /// <summary>
    /// V1-KIT-008: deliberately kept local to this file, not added to the
    /// central ApplicationPermissions.cs catalog — see the task's Goal for
    /// why. Granted to 'manager' for now (migration 110) so the endpoint is
    /// testable before V1-IAM-029's Mutfak Sefi role exists; V1-IAM-029
    /// grants the same code to that role additively.
    /// </summary>
    public const string AvailabilitySuspendPermission = "kitchen.availability.suspend";

    /// <summary>
    /// V1-IAM-028: a ticket/item transition to Cancelled still requires
    /// <see cref="TicketMutationPermission"/> (orders.send) — cancelling is
    /// not something a line-cook-only "kitchen-staff" session may do. Every
    /// other target state (Accepted/Preparing/Ready/Served) only requires the
    /// narrower <see cref="ApplicationPermissions.KitchenAdvance"/>, which
    /// every FOH role also holds (additive, no regression) so this never
    /// changes what a waiter/cashier/supervisor/manager session can do.
    ///
    /// Independent review (2026-09-13) fixed a whitespace-trimming gap here
    /// (a bare string.Equals did not trim like the domain parse does). A
    /// second, independent review (2026-09-14) found the same class of bug
    /// survived in a different shape and empirically proved it: this used
    /// to be a bare string comparison against the literal "Cancelled",
    /// while the actual domain parse
    /// (<c>KitchenOperationsStore.ParseEnum</c>, backed by
    /// <see cref="Enum.TryParse{TEnum}(string?, bool, out TEnum)"/>) also
    /// accepts an enum's raw numeric value — <c>TargetState: "4"</c> parses
    /// to <see cref="KitchenTicketState.Cancelled"/> (ordinal 4) exactly as
    /// well as the string "Cancelled" does. A request sending "4" was gated
    /// as non-Cancelled (kitchen.advance only) while the store still
    /// actually cancelled the ticket — a kitchen-staff-only session could
    /// bypass the orders.send requirement with a numeric target state.
    /// This is now generic and calls the identical
    /// <c>Enum.TryParse</c>/<c>Enum.IsDefined</c> pair <c>ParseEnum</c>
    /// itself uses, so this check's notion of "is this a Cancelled
    /// request" can never diverge from what the store will actually parse
    /// again, in any representation.
    /// </summary>
    private static string TicketTransitionPermission<TTargetState>(string? targetState)
        where TTargetState : struct, Enum
    {
        var isCancelled = Enum.TryParse<TTargetState>(targetState, true, out var parsed)
            && Enum.IsDefined(parsed)
            && string.Equals(parsed.ToString(), nameof(KitchenTicketState.Cancelled), StringComparison.Ordinal);
        return isCancelled ? TicketMutationPermission : ApplicationPermissions.KitchenAdvance;
    }

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
        services.TryAddSingleton<IPrintQueueService, PrintQueueService>();
        services.TryAddSingleton<IPhysicalPrintRecoveryRepository, PostgresPhysicalPrintRecoveryRepository>();
        services.TryAddSingleton<IPhysicalPrintRecoveryService, PhysicalPrintRecoveryService>();
        // V1-RMD-130: found by an independent audit (2026-09-09) — every
        // collaborator above this line already existed, well-tested, but
        // nothing ever actually dispatched a print job to a real printer.
        services.TryAddSingleton<IPrinterTransport, TcpEscPosPrinterTransport>();
        services.AddHostedService<KitchenPrintDispatchHostedService>();
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
        // V1-WTR-009: broadcasts "ready" to every connected waiter device.
        services.TryAddSingleton<IOrderRepository, PostgresOrderRepository>();
        services.AddWaiterNotificationsExperience();
        // V1-KIT-008: SuspendProductAvailabilityAsync calls Catalog's own
        // CatalogManagementStore (Scoped — see AddCatalogManagement), so
        // KitchenOperationsStore itself can no longer be a Singleton (a
        // Scoped dependency would be captured for the app's lifetime
        // otherwise). Every existing consumer resolves it per-request from
        // an endpoint delegate, so Scoped is safe here.
        services.TryAddScoped<KitchenOperationsStore>();
        services.TryAddSingleton<IKitchenOperationsSessionAuthorizer, KitchenOperationsSessionAuthorizer>();
        services.AddCatalogManagement();
        services.TryAddSingleton<IAuthorizationGrantRepository, PostgresAuthorizationGrantRepository>();
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
                context, terminalId, TicketTransitionPermission<KitchenTicketState>(request.TargetState), cancellationToken);
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
                context, terminalId, TicketTransitionPermission<KitchenTicketItemState>(request.TargetState), cancellationToken);
            return Results.Ok(await store.TransitionItemAsync(ticketId, itemId, request, cancellationToken));
        });

        group.MapPost("/tickets/{ticketId:guid}/items/{itemId:guid}/undo", async (
            Guid terminalId,
            Guid ticketId,
            Guid itemId,
            UndoKitchenItemV1 request,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            // V1-KIT-009: undo is always a forward-step correction (never a
            // cancel), so it only ever needs kitchen.advance — the same
            // grant a kitchen-staff-only session already has to make the
            // mistake in the first place.
            await authorizer.RequirePermissionAsync(
                context, terminalId, ApplicationPermissions.KitchenAdvance, cancellationToken);
            return Results.Ok(await store.UndoItemAsync(ticketId, itemId, request, cancellationToken));
        });

        // V1-KIT-008: 86 a product from the Kitchen screen itself, under
        // Kitchen's own cashier-session model — see this file's Goal note on
        // AvailabilitySuspendPermission for why this does not just call
        // Catalog's manager-cookie-protected endpoint directly.
        group.MapPost("/products/{productId:guid}/suspend", async (
            Guid terminalId,
            Guid productId,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequirePermissionAsync(
                context, terminalId, AvailabilitySuspendPermission, cancellationToken);
            return Results.Ok(await store.SuspendProductAvailabilityAsync(productId, principal, cancellationToken));
        });

        // V1-RMD-219: found by an independent audit (2026-09-16) - the
        // routing form used to fetch categories from Catalog's own
        // manager-cookie-protected endpoint, which kitchen-chef never
        // holds the cookie for. Same RequireReadAsync as every other GET
        // in this file.
        group.MapGet("/categories", async (
            Guid terminalId,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.GetCategoriesAsync(cancellationToken));
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

        // Found while wiring category-level printer routing end to end
        // (2026-09-12): PUT below only ever edits a route that already
        // exists - nothing could create the first one. routeId is
        // caller-generated (the PosTerminal routing form generates it),
        // same convention as PUT's own path parameter.
        group.MapPost("/routes/{routeId:guid}", async (
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
            return Results.Ok(await store.CreateRouteAsync(routeId, request, cancellationToken));
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

        group.MapGet("/operations/live-sync", async (
            Guid terminalId,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.GetLiveSyncStatusAsync(cancellationToken));
        });

        // V1-KIT-014: research-grounded (docs/engineering/kitchen-allday-view-
        // and-performance-report-research.md) station performance report —
        // reports.view, the same gate /audit/aggregate and /audit/correlation
        // already use, not the plain RequireReadAsync every cashier session
        // passes. Only supervisor/manager hold reports.view today; whether
        // Mutfak Sefi should is a separate, undecided product question.
        group.MapGet("/operations/performance-report", async (
            Guid terminalId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            IKitchenOperationsSessionAuthorizer authorizer,
            KitchenOperationsStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequirePermissionAsync(
                context, terminalId, ApplicationPermissions.ReportsView, cancellationToken);
            if (from is null || to is null)
                throw new ArgumentException("Both 'from' and 'to' query parameters are required.");
            return Results.Ok(await store.GetPerformanceReportAsync(from.Value, to.Value, cancellationToken));
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
            // V1-RMD-116: the system-wide audit trail crosses every module's
            // aggregates (void/comp/discount decisions included), so it needs
            // the same reports.view gate as AuthorizationDecisionEndpoints —
            // any authenticated read (RequireReadAsync) was too broad.
            await authorizer.RequirePermissionAsync(
                context, terminalId, ApplicationPermissions.ReportsView, cancellationToken);
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
            await authorizer.RequirePermissionAsync(
                context, terminalId, ApplicationPermissions.ReportsView, cancellationToken);
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
