using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.Orders.OrderStockConsumption;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.NfcOrdering;

/// <summary>
/// V12-NFC-001. Deliberately unauthenticated — a customer's phone has no
/// cashier/waiter session and none is asked for; the tapped NFC tag's
/// stable per-table link is the only credential, and it only ever resolves
/// on the restaurant's own local network (no relay, see
/// `docs/architecture/qr-relay-provider-decision.md`).
/// </summary>
public static class NfcOrderingEndpoints
{
    public const string RoutePrefix = "/api/v1/nfc/tables/{tableId:guid}";

    public static IServiceCollection AddNfcOrderingExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DualScreenStore>();
        services.TryAddSingleton<IOrderRepository, PostgresOrderRepository>();
        services.TryAddSingleton<SubmitOrderHandler>();
        // Same factory OrderManagementEndpoints registers (V1-RMD-113) —
        // TryAdd, so in the real Host composition (which wires both
        // modules) whichever runs first wins; either way it is the exact
        // same station-id-driven dispatcher, so it makes no behavioural
        // difference which one does.
        services.TryAddSingleton<IKitchenTicketRepository, PostgresKitchenTicketRepository>();
        services.TryAddSingleton<IOrderSubmissionDispatcher>(sp =>
        {
            var stationId = Environment.GetEnvironmentVariable(DualScreenApplication.KitchenStationEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(stationId))
            {
                throw new InvalidOperationException(
                    $"{DualScreenApplication.KitchenStationEnvironmentVariable} is required before NFC order submission is enabled.");
            }

            return new KitchenOrderSubmissionDispatcher(sp.GetRequiredService<IKitchenTicketRepository>(), stationId);
        });
        // V1-RMD-143: Semih's decision (2026-09-09) — NFC's own trusted
        // immediate-accept also consumes stock now (see NfcOrderingStore's
        // own TryConsumeStockAndAcceptAsync and OrderStockConsumptionService's
        // doc comment).
        services.TryAddSingleton<IProductStockMappingRepository, PostgresProductStockMappingRepository>();
        services.TryAddSingleton<IStockItemRepository, PostgresStockItemRepository>();
        services.TryAddSingleton<IStockBalanceRepository, PostgresStockBalanceRepository>();
        services.TryAddSingleton<IStockMovementRepository, PostgresStockMovementRepository>();
        services.TryAddSingleton<OrderStockConsumptionService>();
        services.TryAddSingleton<NfcOrderingStore>();
        services.TryAddTransient<NfcOrderingExceptionFilter>();
        return services;
    }

    public static RouteGroupBuilder MapNfcOrderingApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("NfcOrdering")
            .RequireRateLimiting("nfc-order")
            .AddEndpointFilter<NfcOrderingExceptionFilter>();

        // V12-NFC-003: the same read-only projection the (authenticated)
        // terminal catalog endpoint already serves
        // (DualScreenApplication.Endpoints.cs) — reused as-is rather than
        // reimplemented, just without the cashier-session requirement. A
        // product listing carries nothing sensitive; tableId is unused by
        // the query itself but kept in the path so this stays under the
        // same nfc-order rate-limit partition as the order endpoint.
        group.MapGet("/catalog", async (
            Guid tableId,
            string? category,
            string? limit,
            string? cursor,
            DualScreenStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            _ = tableId;
            var pageSize = DualScreenStore.ParseCatalogLimit(limit);
            var page = await store.GetCatalogAsync(category, pageSize, cursor, cancellationToken);
            if (page.NextCursor is not null)
                context.Response.Headers["X-Next-Cursor"] = page.NextCursor;
            return Results.Ok(page.Items);
        });

        group.MapPost("/orders", async (
            Guid tableId,
            NfcOrderRequest request,
            NfcOrderingStore store,
            CancellationToken cancellationToken) =>
        {
            // V1-RMD-136: found by an independent audit (2026-09-09) — these
            // three inline BadRequest branches ran before
            // NfcOrderingExceptionFilter and returned raw English literals
            // straight to the customer's phone screen (NfcOrder.tsx renders
            // `message` verbatim), the same class of leak V1-RMD-127 fixed
            // in OrderManagementEndpoints.cs the same day — that pass simply
            // never looked at this file.
            if (tableId == Guid.Empty)
                return Results.BadRequest(new { error = new { code = "INVALID_TABLE", message = "Masa kimliği boş olamaz." } });

            if (request.Items == null || request.Items.Count == 0)
                return Results.BadRequest(new { error = new { code = "EMPTY_ITEMS", message = "Sipariş kalemleri boş olamaz." } });

            if (request.Id == Guid.Empty)
                return Results.BadRequest(new { error = new { code = "INVALID_SUBMISSION_ID", message = "Gönderim kimliği boş olamaz." } });

            var order = await store.PlaceOrderAsync(tableId, request, cancellationToken);
            return Results.Ok(order);
        });

        return group;
    }
}

/// <summary>Mirrors OrderManagementExceptionFilter's mapping (V1-ORD-005 pattern) for this unauthenticated surface.</summary>
public sealed class NfcOrderingExceptionFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5300, nameof(LogRequestFailure)),
            "NFC ordering request failed on {Path} ({TraceIdentifier}).");

    private readonly ILogger<NfcOrderingExceptionFilter> _logger;

    public NfcOrderingExceptionFilter(ILogger<NfcOrderingExceptionFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
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
                LogRequestFailure(_logger, context.HttpContext.Request.Path, context.HttpContext.TraceIdentifier, exception);
            }

            return Results.Json(
                new { error = new { code = mapped.Code, message = mapped.Message } },
                statusCode: mapped.Status);
        }
    }

    private static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        NfcTableNotFoundException => (404, "TABLE_NOT_FOUND", "Masa bulunamadı."),
        NfcTableNotAvailableException => (409, "TABLE_NOT_AVAILABLE", "Bu masada şu anda kendi kendine sipariş verilemiyor, lütfen garsonu çağırın."),
        KeyNotFoundException => (400, "PRODUCT_NOT_FOUND", "Seçilen ürün bulunamadı veya artık satışta değil."),
        SubmitOrderIdempotencyConflictException => (409, "IDEMPOTENCY_KEY_REUSED", "Bu işlem anahtarı farklı bir istek için zaten kullanılmış."),
        StaleOrderVersionException or InvalidOperationException => (409, "CONCURRENCY_CONFLICT", "Sipariş başka bir işlem tarafından değiştirildi."),
        ArgumentException or BadHttpRequestException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        PostgresException or NpgsqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };
}
