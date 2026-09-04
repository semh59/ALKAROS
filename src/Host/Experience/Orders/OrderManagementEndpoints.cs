using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Orders.ItemExceptions;
using ALKAROS.Orders.OrderAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.Orders;

public static class OrderManagementEndpoints
{
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/orders";
    public const string CashierCookieName = DualScreenApplication.CashierCookieName;

    public static IServiceCollection AddOrderManagementExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // V1-ORD-005: was missing — every endpoint in this group takes a
        // DualScreenStore parameter, but nothing registered it here (it only
        // worked when the full Host composition happened to register it
        // first). Minimal API can't infer an unregistered service parameter
        // and fails route building with "Failure to infer one or more
        // parameters", so a standalone host for this module never started.
        services.TryAddSingleton<DualScreenStore>();
        services.TryAddSingleton<IOrderRepository, PostgresOrderRepository>();
        services.TryAddSingleton<OrderManagementStore>();
        services.TryAddSingleton<IRoleRepository, PostgresRoleRepository>();
        services.TryAddSingleton<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddSingleton<IAuthorizationService, AuthorizationService>();
        // V1-ORD-005: wires the existing (previously unreachable)
        // ItemExceptionHandler.VoidItemAsync to the pre-send void endpoint.
        services.TryAddSingleton<ItemExceptionHandler>();
        // V1-ORD-005: every endpoint in this group calls RequireCashierSessionAsync
        // (or the permission variant), which throws DualScreenUnauthorizedException
        // on a missing/invalid session — with no filter that unwound as a bare 500,
        // not the 401 the caller needs. Same fix already exists per-module for
        // Billing/Catalog/Kitchen/Tables/Authorization; Orders never got it.
        services.TryAddTransient<OrderManagementExceptionFilter>();
        return services;
    }

    public static RouteGroupBuilder MapOrderManagementApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("Orders")
            .RequireRateLimiting("terminal-write")
            .AddEndpointFilter<OrderManagementExceptionFilter>();

        group.MapPost("/table-draft", async (
            Guid terminalId,
            CreateTableDraftRequest request,
            OrderManagementStore store,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.OrdersCreate, cancellationToken);

            if (request.TableId == Guid.Empty)
                return Results.BadRequest(new { error = new { code = "INVALID_TABLE", message = "TableId cannot be empty." } });

            if (request.Items == null || request.Items.Count == 0)
                return Results.BadRequest(new { error = new { code = "EMPTY_ITEMS", message = "Order items cannot be empty." } });

            var draft = await store.CreateOrUpdateTableDraftAsync(request, cancellationToken);
            return Results.Ok(draft);
        });

        group.MapGet("/table/{tableId:guid}", async (
            Guid terminalId,
            Guid tableId,
            OrderManagementStore store,
            DualScreenStore dualStore,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierSessionAsync(context, terminalId, dualStore, cancellationToken);

            var order = await store.GetActiveOrderByTableIdAsync(tableId, cancellationToken);
            if (order == null)
                return Results.NotFound(new { error = new { code = "ORDER_NOT_FOUND", message = "No active order for table." } });

            return Results.Ok(order);
        });

        group.MapGet("/{orderId:guid}", async (
            Guid terminalId,
            Guid orderId,
            OrderManagementStore store,
            DualScreenStore dualStore,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierSessionAsync(context, terminalId, dualStore, cancellationToken);

            var order = await store.GetOrderByIdAsync(orderId, cancellationToken);
            if (order == null)
                return Results.NotFound(new { error = new { code = "ORDER_NOT_FOUND", message = "Order not found." } });

            return Results.Ok(order);
        });

        group.MapPost("/{orderId:guid}/submit", async (
            Guid terminalId,
            Guid orderId,
            SubmitTableOrderRequest request,
            OrderManagementStore store,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.OrdersSend, cancellationToken);

            try
            {
                var submitted = await store.SubmitOrderAsync(orderId, request.ExpectedRowVersion, cancellationToken);
                return Results.Ok(submitted);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = new { code = "ORDER_NOT_FOUND", message = "Order not found." } });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = new { code = "CONCURRENCY_CONFLICT", message = ex.Message } });
            }
        });

        // V1-ORD-005: ItemExceptionHandler.VoidItemAsync already existed
        // (V1-ORD-003) and works — nothing called it. Gated by orders.create
        // (model §2: unsent items need only orders.create, not the grant-class
        // bills.void); VoidItemAsync itself still refuses anything already
        // sent to the kitchen (V0-DOM-006's default wall, unchanged for this
        // path — see V1-IAM-027 for the grant-gated exception).
        group.MapPost("/{orderId:guid}/items/{itemId:guid}/void", async (
            Guid terminalId,
            Guid orderId,
            Guid itemId,
            VoidOrderItemRequestV1 request,
            ItemExceptionHandler itemExceptions,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.OrdersCreate, cancellationToken);

            try
            {
                var command = new VoidOrderItemCommand(
                    orderId,
                    itemId,
                    request.ExpectedRowVersion,
                    userId,
                    IsManagerAuthorized: true,
                    request.ReasonCode,
                    CorrelationId: context.TraceIdentifier,
                    request.Notes);
                var result = await itemExceptions.VoidItemAsync(command, cancellationToken);
                return Results.Ok(new VoidOrderItemResultV1(
                    result.OrderId,
                    result.OrderItemId,
                    result.NewItemStatus.ToString(),
                    result.NewOrderRowVersion,
                    result.NewOrderTotal,
                    result.AppliedAt));
            }
            catch (OrderItemNotFoundException)
            {
                return Results.NotFound(new { error = new { code = "ITEM_NOT_FOUND", message = "Order item not found." } });
            }
            catch (InvalidItemReasonException ex)
            {
                return Results.BadRequest(new { error = new { code = "VALIDATION_FAILED", message = ex.Message } });
            }
            catch (LateVoidRejectedException ex)
            {
                return Results.Conflict(new { error = new { code = "ALREADY_SENT", message = ex.Message } });
            }
            catch (StaleOrderRowVersionException ex)
            {
                return Results.Conflict(new { error = new { code = "CONCURRENCY_CONFLICT", message = ex.Message } });
            }
            catch (InvalidOperationException ex)
            {
                // Order not found, or the item is no longer Active (already
                // voided/comped) — both are "the world moved on", not a bad
                // request.
                return Results.Conflict(new { error = new { code = "CONCURRENCY_CONFLICT", message = ex.Message } });
            }
        });

        return group;
    }

    private static async Task<Guid> RequireCashierSessionAsync(
        HttpContext context, Guid terminalId, DualScreenStore store, CancellationToken cancellationToken)
    {
        var cashierToken = context.Request.Cookies[CashierCookieName];
        if (string.IsNullOrWhiteSpace(cashierToken))
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                cashierToken = authHeader["Bearer ".Length..].Trim();
            }
        }

        var principal = await store.AuthenticateCashierAsync(cashierToken, terminalId, cancellationToken);
        if (principal is null)
            throw new DualScreenUnauthorizedException("Cashier authentication is required.");
        return principal.UserId;
    }

    private static async Task<Guid> RequireCashierPermissionAsync(
        HttpContext context,
        Guid terminalId,
        DualScreenStore store,
        IAuthorizationService authorization,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        var userId = await RequireCashierSessionAsync(context, terminalId, store, cancellationToken);
        await authorization.AuthorizeAsync(userId, permissionCode, cancellationToken);
        return userId;
    }
}

/// <summary>
/// V1-ORD-005: catches what each endpoint's own inline catches don't —
/// principally <see cref="DualScreenUnauthorizedException"/> from the shared
/// session helpers, so a missing/invalid cashier session maps to 401 rather
/// than an unhandled 500. Mirrors the per-module filter already present on
/// Billing/Catalog/Kitchen/Tables/Authorization (e.g. KitchenOperationsExceptionFilter).
/// </summary>
public sealed class OrderManagementExceptionFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5200, nameof(LogRequestFailure)),
            "Order management request failed on {Path} ({TraceIdentifier}).");

    private readonly ILogger<OrderManagementExceptionFilter> _logger;

    public OrderManagementExceptionFilter(ILogger<OrderManagementExceptionFilter> logger)
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
            if (mapped.Status >= StatusCodes.Status500InternalServerError)
            {
                LogRequestFailure(
                    _logger,
                    context.HttpContext.Request.Path,
                    context.HttpContext.TraceIdentifier,
                    exception);
            }

            return Results.Json(
                new { error = new { code = mapped.Code, message = mapped.Message } },
                statusCode: mapped.Status);
        }
    }

    private static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        DualScreenUnauthorizedException => (401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
        AuthorizationDeniedException => (403, "FORBIDDEN", "Bu işlem için yetkiniz yok."),
        KeyNotFoundException or OrderItemNotFoundException => (404, "NOT_FOUND", "İstenen kayıt bulunamadı."),
        InvalidItemReasonException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        LateVoidRejectedException => (409, "ALREADY_SENT", "Ürün zaten mutfağa gönderilmiş."),
        StaleOrderRowVersionException or InvalidOperationException => (409, "CONCURRENCY_CONFLICT", "Sipariş başka bir işlem tarafından değiştirildi."),
        ArgumentException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        PostgresException or NpgsqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };
}
