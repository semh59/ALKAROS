using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.OnlineOrdering.StoreStatus;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.OnlineOrdering;

/// <summary>
/// V12-ONL-011: the online food screen's open/closed switch for a manager (<c>integrations.manage</c>) — per platform,
/// what was asked, whether the platform has been told, and what the platform reports now; a request opens the
/// restaurant, closes it for the rest of the service day, or pauses it as busy for a chosen number of minutes. The
/// request is kept and delivered with retries by <see cref="OnlineStoreStatusHostedService"/>; no platform error text
/// reaches the screen.
/// </summary>
public static class OnlineStoreStatusEndpoints
{
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/online-store-status";

    public static IServiceCollection AddOnlineStoreStatusExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DualScreenStore>();
        services.TryAddSingleton<IRoleRepository, PostgresRoleRepository>();
        services.TryAddSingleton<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddSingleton<IAuthorizationService, AuthorizationService>();
        services.TryAddSingleton(TimeProvider.System);
        // TryAdd defers to OnlineOrderingModule in the real Host, which also registers each platform's channel.
        services.TryAddTransient(provider => new OnlineStoreStatusService(
            provider.GetRequiredService<NpgsqlDataSource>(),
            provider.GetServices<IOnlineStoreStatusChannel>(),
            provider.GetRequiredService<TimeProvider>()));
        services.TryAddTransient<OnlineStoreStatusExceptionFilter>();
        services.AddHostedService<OnlineStoreStatusHostedService>();
        return services;
    }

    public static RouteGroupBuilder MapOnlineStoreStatusApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("OnlineStoreStatus")
            .AddEndpointFilter<OnlineStoreStatusExceptionFilter>();

        group.MapGet("/", async (
            Guid terminalId,
            OnlineStoreStatusService service,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireManagerAsync(context, terminalId, dualStore, authorization, cancellationToken);
            return Results.Ok((await service.StatusAsync(cancellationToken)).Select(ToView).ToList());
        }).RequireRateLimiting("terminal-read");

        group.MapPut("/{provider}", async (
            Guid terminalId,
            string provider,
            SetOnlineStoreStatusRequestV1 request,
            OnlineStoreStatusService service,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireManagerAsync(context, terminalId, dualStore, authorization, cancellationToken);
            var state = request.State switch
            {
                "Open" => OnlineStoreState.Open,
                "ClosedToday" => OnlineStoreState.ClosedToday,
                "Busy" => OnlineStoreState.ClosedUntil,
                _ => throw new InvalidStoreStatusRequestException("InvalidState"),
            };
            var status = await service.RequestAsync(new OnlineStoreStatusRequest(provider, state, request.Minutes), userId, cancellationToken);
            return Results.Ok(ToView(status));
        }).RequireRateLimiting("terminal-write");

        return group;
    }

    private static OnlineStoreStatusV1 ToView(OnlineStoreStatus status) => new(
        status.Provider,
        status.Configured,
        status.DesiredState switch
        {
            OnlineStoreState.Open => "Open",
            OnlineStoreState.ClosedToday => "ClosedToday",
            _ => status.Reason == OnlineStoreCloseReason.Busy ? "Busy" : "ClosedUntil",
        },
        status.ClosedUntil,
        status.Delivered ? "Delivered" : status.DeliveryFailing ? "Retrying" : "Pending",
        status.Platform is null ? "Unknown" : status.Platform.Open ? "Open" : "Closed",
        status.Platform?.ClosedUntil);

    private static async Task<Guid> RequireManagerAsync(
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
        await authorization.AuthorizeAsync(principal.UserId, ApplicationPermissions.IntegrationsManage, cancellationToken);
        return principal.UserId;
    }
}

/// <summary><c>State</c> is <c>Open</c>, <c>ClosedToday</c> or <c>Busy</c>; <c>Minutes</c> is required for, and only for, <c>Busy</c>.</summary>
public sealed record SetOnlineStoreStatusRequestV1(string? State, int? Minutes);

/// <summary>
/// One platform on the screen. <c>State</c>: Open, ClosedToday, Busy or ClosedUntil (a closure the system recorded).
/// <c>Delivery</c>: Delivered, Pending or Retrying. <c>PlatformState</c>: Open, Closed or Unknown (not readable now).
/// </summary>
public sealed record OnlineStoreStatusV1(
    string Provider, bool Configured, string State, DateTimeOffset? ClosedUntil, string Delivery, string PlatformState, DateTimeOffset? PlatformClosedUntil);

/// <summary>Turkish messages for every failure.</summary>
public sealed class OnlineStoreStatusExceptionFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(5571, nameof(LogRequestFailure)),
            "Online store status request failed on {Path} ({TraceIdentifier}).");

    private readonly ILogger<OnlineStoreStatusExceptionFilter> _logger;

    public OnlineStoreStatusExceptionFilter(ILogger<OnlineStoreStatusExceptionFilter> logger)
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
            var (status, code, message) = Map(exception);
            if (status >= StatusCodes.Status500InternalServerError)
                LogRequestFailure(_logger, context.HttpContext.Request.Path, context.HttpContext.TraceIdentifier, exception);
            return Results.Json(new { error = new { code, message } }, statusCode: status);
        }
    }

    public static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        DualScreenUnauthorizedException => (401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
        AuthorizationDeniedException => (403, "FORBIDDEN", "Bu işlem için yetkiniz yok."),
        UnknownStoreStatusPlatformException => (404, "PLATFORM_NOT_FOUND", "Bu online platform tanınmıyor."),
        InvalidStoreStatusRequestException { Message: "NotConfigured" } =>
            (409, "NOT_CONFIGURED", "Bu platformun bağlantı bilgileri eksik; önce Ayarlar'dan girin."),
        InvalidStoreStatusRequestException { Message: "InvalidDuration" } =>
            (400, "INVALID_DURATION", "Yoğunluk süresi 15, 30, 45, 60, 90 veya 120 dakika olmalı."),
        InvalidStoreStatusRequestException or ArgumentException or BadHttpRequestException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        PostgresException or NpgsqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };
}
