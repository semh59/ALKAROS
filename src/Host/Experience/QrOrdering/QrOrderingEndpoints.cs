using ALKAROS.Host.DualScreen;
using ALKAROS.QrOrdering.CustomerSession;
using ALKAROS.QrOrdering.PendingOrders;
using ALKAROS.QrOrdering.RelaySecurity;
using ALKAROS.QrOrdering.TokenLifecycle;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.QrOrdering;

/// <summary>
/// V12-CWB-001. Deliberately public/relay-facing (docs/architecture/
/// qr-relay-provider-decision.md) — there is no LAN-only assumption here the
/// way NFC's own endpoints get to make. The scanned table token is exchanged
/// for a customer session exactly once (<c>/sessions</c>); every other route
/// requires that session token instead of ever resending the raw table
/// token. QR order submission itself (<c>V12-CWB-002</c>) is a later,
/// separate addition to this same route group — this task only opens the
/// "browse the menu" half of the flow (cart submission is explicitly out of
/// CWB-001's own scope).
/// </summary>
public static class QrOrderingEndpoints
{
    public const string RoutePrefix = "/api/v1/qr";

    /// <summary>
    /// The only credential a request needs after session issuance — the raw
    /// table token is never resent. A plain header (not a cookie): this
    /// origin serves no other same-site state a cookie's ambient-credential
    /// behaviour could accidentally leak into, and a header keeps the token
    /// out of server/proxy access logs the way a query string would not.
    /// </summary>
    public const string SessionHeaderName = "X-Alkaros-Qr-Session";

    public static IServiceCollection AddQrOrderingExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DualScreenStore>();
        // Self-contained (matches AddNfcOrderingExperience's own rationale):
        // in the real Host composition these are already registered,
        // Transient, by QrOrderingModule (ModuleRegistry.DefaultCatalog) —
        // TryAdd defers to that. A standalone host that only calls this one
        // extension (as this experience's own HTTP tests do) still resolves
        // the whole chain on its own.
        services.TryAddSingleton<ITableTokenRepository, PostgresTableTokenRepository>();
        services.TryAddSingleton<TableTokenService>();
        services.TryAddSingleton<ICustomerSessionRepository, PostgresCustomerSessionRepository>();
        services.TryAddSingleton<CustomerSessionService>();
        services.TryAddSingleton<IRelayNonceStore, PostgresRelayNonceStore>();
        services.TryAddSingleton<RelayRequestValidator>();
        services.TryAddTransient<QrOrderingExceptionFilter>();
        return services;
    }

    public static RouteGroupBuilder MapQrOrderingApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("QrOrdering")
            .AddEndpointFilter<QrOrderingExceptionFilter>();

        // V12-QRS-002's anti-replay pair guards this one call — the raw
        // table token is a long-lived (up to 4h), URL-embedded secret a
        // customer's own scan exposes to their browser history/screenshots;
        // nonce+timestamp bound how long a captured HTTP request (not the
        // token itself) stays replayable. No route parameter to partition
        // the rate limiter on yet (no session exists), so this policy is
        // keyed by source IP alone (RelayAbusePolicy.PerIpRequestLimit).
        group.MapPost("/sessions", async (
            QrSessionIssueRequest request,
            RelayRequestValidator relayValidator,
            CustomerSessionService sessionService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.TableToken))
                return Results.BadRequest(new { error = new { code = "INVALID_TOKEN", message = "QR kodu okunamadı." } });

            var relayResult = await relayValidator.ValidateAsync(
                request.TableToken, request.Nonce, request.Timestamp, cancellationToken);
            if (!relayResult.IsValid)
                throw new QrTableTokenInvalidException(relayResult.FailureReason!);

            var issue = await sessionService.IssueAsync(request.TableToken, cancellationToken);
            if (!issue.IsValid)
                throw new QrTableTokenInvalidException(issue.FailureReason!);

            return Results.Ok(new QrSessionIssueResponse(issue.RawToken!, issue.TableId!.Value));
        }).RequireRateLimiting("qr-session");

        // The same read-only projection NFC's own catalog endpoint serves
        // (DualScreenApplication.Endpoints.cs), reused as-is — but unlike
        // NFC's, this route requires a valid customer session: CWB-001's own
        // goal is "for an authenticated QR customer session", and the public
        // relay has no LAN-only backstop to fall back on the way NFC does.
        group.MapGet("/menu", async (
            string? category,
            string? limit,
            string? cursor,
            HttpContext context,
            CustomerSessionService sessionService,
            DualScreenStore store,
            CancellationToken cancellationToken) =>
        {
            var rawSession = context.Request.Headers[SessionHeaderName].ToString();
            var validation = await sessionService.ValidateAsync(rawSession, cancellationToken);
            if (!validation.IsValid)
                throw new QrCustomerSessionInvalidException(validation.FailureReason!);

            var pageSize = DualScreenStore.ParseCatalogLimit(limit);
            var page = await store.GetCatalogAsync(category, pageSize, cursor, cancellationToken);
            if (page.NextCursor is not null)
                context.Response.Headers["X-Next-Cursor"] = page.NextCursor;
            return Results.Ok(page.Items);
        }).RequireRateLimiting("qr-order");

        return group;
    }
}

/// <summary>Mirrors NfcOrderingExceptionFilter's mapping (V1-ORD-005 pattern) for this relay-facing surface.</summary>
public sealed class QrOrderingExceptionFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5500, nameof(LogRequestFailure)),
            "QR ordering request failed on {Path} ({TraceIdentifier}).");

    private readonly ILogger<QrOrderingExceptionFilter> _logger;

    public QrOrderingExceptionFilter(ILogger<QrOrderingExceptionFilter> logger)
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
        QrTableTokenInvalidException tokenException => MapTableTokenReason(tokenException.Reason),
        QrCustomerSessionInvalidException sessionException => MapSessionReason(sessionException.Reason),
        ArgumentException or BadHttpRequestException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        PostgresException or NpgsqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };

    private static (int Status, string Code, string Message) MapTableTokenReason(string reason) => reason switch
    {
        "REPLAYED" => (409, "QR_REQUEST_REPLAYED", "Bu istek zaten işlendi, lütfen sayfayı yenileyip tekrar deneyin."),
        "TIMESTAMP_OUT_OF_WINDOW" => (401, "QR_REQUEST_EXPIRED", "İstek zaman aşımına uğradı, lütfen sayfayı yenileyip tekrar deneyin."),
        "REVOKED" => (401, "QR_TOKEN_REVOKED", "Bu QR kodu artık geçerli değil, lütfen masadaki güncel kodu okutun."),
        "EXPIRED" => (401, "QR_TOKEN_EXPIRED", "Bu QR kodunun süresi doldu, lütfen masadaki güncel kodu okutun."),
        _ => (401, "QR_TOKEN_INVALID", "QR kodu tanınmadı, lütfen masadaki kodu tekrar okutun."),
    };

    private static (int Status, string Code, string Message) MapSessionReason(string reason) => reason switch
    {
        "IDLE_EXPIRED" => (401, "QR_SESSION_IDLE_EXPIRED", "Uzun süre işlem yapılmadığı için oturumunuz sona erdi, lütfen QR kodunu tekrar okutun."),
        "REVOKED" => (401, "QR_SESSION_REVOKED", "Oturumunuz sonlandırıldı, lütfen QR kodunu tekrar okutun."),
        "EXPIRED" => (401, "QR_SESSION_EXPIRED", "Oturumunuzun süresi doldu, lütfen QR kodunu tekrar okutun."),
        _ => (401, "QR_SESSION_INVALID", "Oturumunuz bulunamadı, lütfen QR kodunu tekrar okutun."),
    };
}

/// <summary>V12-CWB-001: the raw table token failed relay/token validation (unknown/revoked/expired/replayed/stale timestamp).</summary>
public sealed class QrTableTokenInvalidException : Exception
{
    public string Reason { get; }

    public QrTableTokenInvalidException(string reason)
        : base($"Table token is invalid: {reason}.")
    {
        Reason = reason;
    }
}
