using ALKAROS.Host.DualScreen;
using ALKAROS.QrOrdering.CustomerSession;
using ALKAROS.QrOrdering.PendingOrders;
using ALKAROS.QrOrdering.RelaySecurity;
using ALKAROS.QrOrdering.TablePolicy;
using ALKAROS.QrOrdering.TokenLifecycle;
using ALKAROS.Settings.BusinessIdentity;
using ALKAROS.Settings.GarsonFeatureToggles;
using ALKAROS.Settings.TypedSettings;
using ALKAROS.Tables.TableLifecycle;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data.Common;

namespace ALKAROS.Host.Experience.QrOrdering;

/// <summary>
/// V12-CWB-001/V12-CWB-002. Deliberately public/relay-facing (docs/
/// architecture/qr-relay-provider-decision.md) — there is no LAN-only
/// assumption here the way NFC's own endpoints get to make. The scanned
/// table token is exchanged for a customer session exactly once
/// (<c>/sessions</c>); every other route requires that session token instead
/// of ever resending the raw table token. <c>/sessions</c> and
/// <c>/menu</c> are V12-CWB-001's own "browse the menu" half of the flow;
/// <c>/orders</c> (submit and poll) is V12-CWB-002's later addition to the
/// same route group — cart submission was explicitly out of CWB-001's own
/// scope.
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
        // V12-CWB-002: order submission's own chain — ITableRepository is
        // Table Management's own service (already registered by
        // AddTableManagementExperience/TablesModule in the real Host; TryAdd
        // defers to that), consumed through the approved QrOrdering ->
        // Tables edge (V0-ARC-001 row 19), same as QrOrderingModule.Register
        // itself does not re-register it.
        services.TryAddSingleton<ITableRepository, PostgresTableRepository>();
        services.TryAddSingleton<QrTableReservationPolicy>();
        services.TryAddSingleton<QrPendingOrderStore>();
        services.TryAddTransient<QrOrderingExceptionFilter>();
        // V1-SET-004: /bill checks GarsonFeature.GuestLiveBill — same
        // DbDataSource/ISettingValidator gap every other settings-backed
        // experience registration already had to close on its own.
        services.TryAddSingleton<DbDataSource>(serviceProvider =>
            serviceProvider.GetRequiredService<NpgsqlDataSource>());
        services.TryAddSingleton<ISettingValidator, SettingValidator>();
        services.TryAddSingleton<ISettingsRepository, PostgresSettingsRepository>();
        services.TryAddSingleton<ISettingsService, SettingsService>();
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

        // V1-SET-007: deliberately public, unlike every other route in this
        // group — a business's own name/color is not sensitive, and the
        // very first paint of the QR page needs it before a customer
        // session exists (issuing one costs a relay round trip this page
        // should not have to wait on just to know its own colors).
        group.MapGet("/branding", async (
            ISettingsService settings,
            CancellationToken cancellationToken) =>
        {
            var name = await BusinessNameSetting.GetNameAsync(settings, cancellationToken);
            var theme = await BusinessAccentThemeSetting.GetThemeAsync(settings, cancellationToken);
            return Results.Ok(new QrBrandingResponse(name, theme.Hex, HasLogo: false));
        }).RequireRateLimiting("qr-order");

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

        // V1-WTR-018: garson-karsilastirma idea #6 — a guest polls their own
        // table's running tab from their own phone, same session-header
        // pattern as /menu. Read-only:
        // no write route exists on this DTO's shape, matching CWB-001's own
        // "browse, don't mutate" scope for everything except /orders.
        group.MapGet("/bill", async (
            HttpContext context,
            CustomerSessionService sessionService,
            DualScreenStore store,
            ISettingsService settings,
            CancellationToken cancellationToken) =>
        {
            var rawSession = context.Request.Headers[SessionHeaderName].ToString();
            var validation = await sessionService.ValidateAsync(rawSession, cancellationToken);
            if (!validation.IsValid)
                throw new QrCustomerSessionInvalidException(validation.FailureReason!);

            // V1-SET-004: this deployment turned the guest live-bill feature
            // off — the same empty state the guest's own page already
            // renders before anything has been ordered, not an error.
            if (!await GarsonFeatureToggles.IsEnabledAsync(settings, GarsonFeature.GuestLiveBill, cancellationToken))
                return Results.Ok(QrLiveBillDto.Empty);

            var bill = await store.GetLiveBillAsync(validation.TableId!.Value, cancellationToken);
            return Results.Ok(bill);
        }).RequireRateLimiting("qr-order");

        // V12-CWB-002. Accepted, not Ok: the real Order does not exist yet —
        // QrPendingOrderStore only queues a QrOrderSubmitted integration
        // event; Order's own QrOrderSubmittedConsumer materializes it
        // asynchronously (V0-ARC-001 row 19, the only approved QR->Order
        // integration event). Idempotent on request.SubmissionId, the same
        // client-generated-correlation-id pattern NfcOrderRequest uses — a
        // retry after a dropped connection or a double tap replays the
        // original queued result instead of submitting twice.
        group.MapPost("/orders", async (
            QrOrderSubmissionRequest request,
            HttpContext context,
            QrPendingOrderStore store,
            CancellationToken cancellationToken) =>
        {
            if (request.Items is null || request.Items.Count == 0)
                return Results.BadRequest(new { error = new { code = "EMPTY_ITEMS", message = "Sipariş kalemleri boş olamaz." } });
            if (request.SubmissionId == Guid.Empty)
                return Results.BadRequest(new { error = new { code = "INVALID_SUBMISSION_ID", message = "Gönderim kimliği boş olamaz." } });

            var rawSession = context.Request.Headers[SessionHeaderName].ToString();
            var result = await store.SubmitAsync(rawSession, request, cancellationToken);
            return Results.Accepted(value: new QrOrderSubmissionResponse(result.SubmissionId, result.TableId, result.SubmittedAt));
        }).RequireRateLimiting("qr-order");

        // Polled by the customer's browser after a 202 while the outbox
        // delivery to Order's consumer is still in flight — re-validates the
        // session on every call (sliding its idle window, same as /menu)
        // rather than trusting a tableId the client could otherwise supply
        // directly.
        group.MapGet("/orders/{submissionId:guid}", async (
            Guid submissionId,
            HttpContext context,
            CustomerSessionService sessionService,
            QrPendingOrderStore store,
            CancellationToken cancellationToken) =>
        {
            var rawSession = context.Request.Headers[SessionHeaderName].ToString();
            var validation = await sessionService.ValidateAsync(rawSession, cancellationToken);
            if (!validation.IsValid)
                throw new QrCustomerSessionInvalidException(validation.FailureReason!);

            var outcome = await store.FindResultingOrderAsync(validation.TableId!.Value, submissionId, cancellationToken);
            return Results.Ok(outcome is null
                ? new QrOrderPollResponse(submissionId, "Pending", null)
                : new QrOrderPollResponse(submissionId, outcome.Status, outcome.OrderId));
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
        QrTableNotFoundException => (404, "TABLE_NOT_FOUND", "Masa bulunamadı."),
        QrTableNotAvailableException => (409, "TABLE_NOT_AVAILABLE", "Bu masada şu anda kendi kendine sipariş verilemiyor, lütfen garsonu çağırın."),
        QrOrderInvalidProductException => (400, "PRODUCT_NOT_FOUND", "Seçilen ürün bulunamadı veya artık satışta değil."),
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
