using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.OnlineOrdering.AvailabilityPublishing;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;
using ALKAROS.Orders.SubmitOrder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.OnlineOrdering;

/// <summary>
/// V12-OUI-001: the operations queue for QR orders waiting for staff and online-channel orders, with
/// the problems and retries behind them. Reading is a plain cross-schema read (V0-ARC-001). Every
/// action goes through the contract that owns it — a QR order through the existing accept/reject
/// endpoints (PendingOrderConfirmationStore), an online order through the V12-ONL-003 status sync —
/// and carries the row version the operator saw, so an action on an outdated screen changes nothing.
/// Same permission as accepting or rejecting a QR order (<c>orders.create</c>).
/// </summary>
public static class OnlineOperationsEndpoints
{
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/online-operations";

    private const int MaxRows = 200;

    public static IServiceCollection AddOnlineOperationsExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DualScreenStore>();
        services.TryAddSingleton<IRoleRepository, PostgresRoleRepository>();
        services.TryAddSingleton<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddSingleton<IAuthorizationService, AuthorizationService>();
        services.TryAddTransient<OnlineOperationsExceptionFilter>();
        return services;
    }

    public static RouteGroupBuilder MapOnlineOperationsApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("OnlineOperations")
            .AddEndpointFilter<OnlineOperationsExceptionFilter>();

        group.MapGet("/", async (
            Guid terminalId,
            string? source,
            NpgsqlDataSource dataSource,
            AvailabilityPublicationService availability,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireStaffAsync(context, terminalId, dualStore, authorization, cancellationToken);
            var sourceFilter = (source ?? "all") switch
            {
                "all" => (string?)null,
                "qr" => "Qr",
                "online" => "Online",
                _ => throw new ArgumentException("Unknown source filter.", nameof(source))
            };

            var orders = await ReadOrdersAsync(dataSource, sourceFilter, cancellationToken);
            var problems = sourceFilter == "Qr" ? [] : await ReadProblemsAsync(dataSource, cancellationToken);
            var retries = await ReadRetriesAsync(dataSource, availability, cancellationToken);
            return Results.Ok(new OnlineOperationsQueueV1(orders, problems, retries));
        }).RequireRateLimiting("terminal-read");

        group.MapPost("/orders/{orderId:guid}/hand-over", async (
            Guid terminalId,
            Guid orderId,
            OnlineOrderActionRequestV1 request,
            YemeksepetiStatusSyncService sync,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireStaffAsync(context, terminalId, dualStore, authorization, cancellationToken);
            return Reply(await sync.HandOverAsync(orderId, userId, request.ExpectedRowVersion, cancellationToken));
        }).RequireRateLimiting("terminal-write");

        group.MapPost("/orders/{orderId:guid}/cancel", async (
            Guid terminalId,
            Guid orderId,
            CancelOnlineOrderRequestV1 request,
            YemeksepetiStatusSyncService sync,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireStaffAsync(context, terminalId, dualStore, authorization, cancellationToken);
            if (!Enum.TryParse<YemeksepetiCancellationReason>(request.Reason, ignoreCase: false, out var reason)
                || !Enum.IsDefined(reason))
                throw new ArgumentException("Unknown cancellation reason.", nameof(request));
            return Reply(await sync.CancelByRestaurantAsync(orderId, reason, userId, request.ExpectedRowVersion, cancellationToken));
        }).RequireRateLimiting("terminal-write");

        return group;
    }

    private static IResult Reply(OnlineOrderActionOutcome outcome) => outcome switch
    {
        OnlineOrderActionOutcome.Applied or OnlineOrderActionOutcome.AlreadyApplied =>
            Results.Ok(new OnlineOrderActionResultV1(outcome.ToString())),
        OnlineOrderActionOutcome.Stale => Results.Json(
            new { error = new { code = "CONCURRENCY_CONFLICT", message = "Sipariş bu ekran açıldıktan sonra değişti. Listeyi yenileyin." } },
            statusCode: StatusCodes.Status409Conflict),
        OnlineOrderActionOutcome.NotAllowed => Results.Json(
            new { error = new { code = "ORDER_STATE_CONFLICT", message = "Siparişin şu anki durumunda bu işlem yapılamaz." } },
            statusCode: StatusCodes.Status409Conflict),
        _ => throw new InvalidOperationException($"Unhandled online order action outcome '{outcome}'.")
    };

    private static async Task<IReadOnlyList<OnlineOperationsOrderV1>> ReadOrdersAsync(
        NpgsqlDataSource dataSource, string? source, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT o.order_id, o.source, o.status, o.order_number, t.table_number,
                   (SELECT i.outcome_detail->>'displayCode' FROM online_ordering.yemeksepeti_webhook_inbox i
                    WHERE i.order_id = o.order_id AND i.processing_outcome = 'OrderCreated' LIMIT 1),
                   o.total,
                   (SELECT count(*) FROM orders.order_items oi WHERE oi.order_id = o.order_id AND oi.status <> 'Cancelled')::int,
                   o.created_at, o.row_version
            FROM orders.orders o
            LEFT JOIN table_mgmt.tables t ON t.table_id = o.table_id
            WHERE ((o.source = 'Qr' AND o.status = 'PendingConfirmation')
                   OR (o.source = 'Online' AND o.status IN ('Accepted', 'Preparing', 'Ready')))
              AND ($1::text IS NULL OR o.source = $1)
            ORDER BY o.created_at, o.order_id
            LIMIT $2;
            """);
        command.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, (object?)source ?? DBNull.Value);
        command.Parameters.AddWithValue(MaxRows);

        var orders = new List<OnlineOperationsOrderV1>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            orders.Add(new OnlineOperationsOrderV1(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetDecimal(6), reader.GetInt32(7), reader.GetFieldValue<DateTimeOffset>(8), reader.GetInt64(9)));
        }

        return orders;
    }

    /// <summary>Provider events of the last day that ended without an order, or keep failing.</summary>
    private static async Task<IReadOnlyList<OnlineOperationsProblemV1>> ReadProblemsAsync(
        NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT inbox_id, external_order_id, provider_status,
                   COALESCE(processing_outcome, 'Retrying'),
                   COALESCE(outcome_detail->>'rejection', outcome_detail->>'reason'),
                   processing_attempts, received_at
            FROM online_ordering.yemeksepeti_webhook_inbox
            WHERE received_at > now() - interval '1 day'
              AND (processing_outcome IN ('Rejected', 'Diverged', 'Failed')
                   OR (processed_at IS NULL AND processing_attempts > 0))
            ORDER BY received_at DESC, inbox_id
            LIMIT $1;
            """);
        command.Parameters.AddWithValue(MaxRows);

        var problems = new List<OnlineOperationsProblemV1>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            problems.Add(new OnlineOperationsProblemV1(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetInt32(5), reader.GetFieldValue<DateTimeOffset>(6)));
        }

        return problems;
    }

    private static async Task<OnlineOperationsRetryV1> ReadRetriesAsync(
        NpgsqlDataSource dataSource, AvailabilityPublicationService availability, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT (SELECT count(*) FROM online_ordering.yemeksepeti_webhook_inbox WHERE processed_at IS NULL)::int,
                   (SELECT count(*) FROM online_ordering.catalog_publications WHERE status = 'Pending' AND delivery_attempts > 0)::int;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var pendingEvents = reader.GetInt32(0);
        var publicationsRetrying = reader.GetInt32(1);
        var divergences = await availability.FindDivergencesAsync(TimeSpan.FromMinutes(5), failedAttempts: 3, limit: MaxRows, cancellationToken);
        return new OnlineOperationsRetryV1(pendingEvents, publicationsRetrying, divergences.Count);
    }

    private static async Task<Guid> RequireStaffAsync(
        HttpContext context, Guid terminalId, DualScreenStore store, IAuthorizationService authorization, CancellationToken cancellationToken)
    {
        var cashierToken = context.Request.Cookies[DualScreenApplication.CashierCookieName];
        if (string.IsNullOrWhiteSpace(cashierToken))
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                cashierToken = authHeader["Bearer ".Length..].Trim();
        }

        var principal = await store.AuthenticateCashierAsync(cashierToken, terminalId, cancellationToken)
            ?? throw new DualScreenUnauthorizedException("Cashier authentication is required.");
        await authorization.AuthorizeAsync(principal.UserId, ApplicationPermissions.OrdersCreate, cancellationToken);
        return principal.UserId;
    }
}

public sealed record OnlineOperationsOrderV1(
    Guid OrderId,
    string Source,
    string Status,
    string OrderNumber,
    string? TableNumber,
    string? DisplayCode,
    decimal Total,
    int ItemCount,
    DateTimeOffset CreatedAt,
    long RowVersion);

public sealed record OnlineOperationsProblemV1(
    Guid InboxId,
    string ExternalOrderId,
    string ProviderStatus,
    string Outcome,
    string? Reason,
    int Attempts,
    DateTimeOffset ReceivedAt);

public sealed record OnlineOperationsRetryV1(int PendingProviderEvents, int CatalogPublicationsRetrying, int AvailabilityDivergences);

public sealed record OnlineOperationsQueueV1(
    IReadOnlyList<OnlineOperationsOrderV1> Orders,
    IReadOnlyList<OnlineOperationsProblemV1> Problems,
    OnlineOperationsRetryV1 Retries);

public sealed record OnlineOrderActionRequestV1(long ExpectedRowVersion);

public sealed record CancelOnlineOrderRequestV1(long ExpectedRowVersion, string Reason);

public sealed record OnlineOrderActionResultV1(string Outcome);

public sealed class OnlineOperationsExceptionFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5560, nameof(LogRequestFailure)),
            "Online operations request failed on {Path} ({TraceIdentifier}).");

    private readonly ILogger<OnlineOperationsExceptionFilter> _logger;

    public OnlineOperationsExceptionFilter(ILogger<OnlineOperationsExceptionFilter> logger)
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
                LogRequestFailure(_logger, context.HttpContext.Request.Path, context.HttpContext.TraceIdentifier, exception);
            return Results.Json(new { error = new { code = mapped.Code, message = mapped.Message } }, statusCode: mapped.Status);
        }
    }

    private static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        DualScreenUnauthorizedException => (401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
        AuthorizationDeniedException => (403, "FORBIDDEN", "Bu işlem için yetkiniz yok."),
        OrderNotFoundException => (404, "ORDER_NOT_FOUND", "Sipariş bulunamadı."),
        ArgumentException or BadHttpRequestException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        NotAnOnlineOrderException => (409, "NOT_AN_ONLINE_ORDER", "Bu işlem yalnız online kanal siparişleri için yapılabilir."),
        InvalidOperationException => (409, "CONCURRENCY_CONFLICT", "Sipariş başka bir işlem tarafından değiştirildi."),
        PostgresException or NpgsqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };
}
