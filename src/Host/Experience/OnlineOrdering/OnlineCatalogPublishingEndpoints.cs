using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.OnlineOrdering.CatalogPublishing;
using ALKAROS.OnlineOrdering.CatalogPublishing.Yemeksepeti;
using ALKAROS.OnlineOrdering.Yemeksepeti.StatusSync;
using ALKAROS.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.OnlineOrdering;

/// <summary>
/// V12-ONL-004: a manager publishes a menu to an online channel. Same authorization as the other
/// integration settings (<c>integrations.manage</c>). The publication is committed here; delivery to
/// the provider happens through the outbox (UNVERIFIED DRAFT provider client).
/// </summary>
public static class OnlineCatalogPublishingEndpoints
{
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/online-ordering/catalog-publications";

    public static IServiceCollection AddOnlineCatalogPublishingExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DualScreenStore>();
        services.TryAddSingleton<IRoleRepository, PostgresRoleRepository>();
        services.TryAddSingleton<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddSingleton<IAuthorizationService, AuthorizationService>();
        services.TryAddSingleton(TimeProvider.System);
        // Same TryAdd-defers-to-OnlineOrderingModule shape as the webhook experience; the mapping
        // service and catalog repositories come from AddYemeksepetiWebhookExperience.
        services.TryAddSingleton<IYemeksepetiPartnerClient>(provider => new YemeksepetiPartnerHttpClient(
            new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
            provider.GetRequiredService<ISecretProvider>(),
            TimeProvider.System));
        services.TryAddEnumerable(ServiceDescriptor.Transient<ICatalogChannelPublisher, YemeksepetiCatalogPublisher>());
        services.TryAddTransient<CatalogPublicationService>();
        services.TryAddTransient<OnlineCatalogPublishingExceptionFilter>();
        return services;
    }

    public static RouteGroupBuilder MapOnlineCatalogPublishingApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("OnlineCatalogPublishing")
            .RequireRateLimiting("terminal-write")
            .AddEndpointFilter<OnlineCatalogPublishingExceptionFilter>();

        group.MapPost("/", async (
            Guid terminalId,
            PublishCatalogRequest request,
            CatalogPublicationService publications,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireManagerAsync(context, terminalId, dualStore, authorization, cancellationToken);
            var summary = await publications.RequestAsync(request.Channel, request.MenuId, userId, cancellationToken);
            return Results.Ok(new PublishCatalogResponse(
                summary.PublicationId,
                summary.Status.ToString(),
                summary.ItemCount,
                summary.ValidationErrors.Select(e => new CatalogValidationErrorResponse(e.ProductId, e.Code.ToString())).ToList(),
                summary.UnsupportedCapabilities.Select(c => c.ToString()).ToList()));
        });

        return group;
    }

    private static async Task<Guid> RequireManagerAsync(
        HttpContext context,
        Guid terminalId,
        DualScreenStore store,
        IAuthorizationService authorization,
        CancellationToken cancellationToken)
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

/// <summary>Machine codes for the client; the Turkish message is what a person may see (docs/UI_STYLE_GUIDE.md).</summary>
public sealed record PublishCatalogRequest(string Channel, Guid MenuId);

public sealed record CatalogValidationErrorResponse(Guid ProductId, string Code);

public sealed record PublishCatalogResponse(
    Guid PublicationId,
    string Status,
    int ItemCount,
    IReadOnlyList<CatalogValidationErrorResponse> ValidationErrors,
    IReadOnlyList<string> UnsupportedCapabilities);

public sealed class OnlineCatalogPublishingExceptionFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5540, nameof(LogRequestFailure)),
            "Online catalog publishing request failed on {Path} ({TraceIdentifier}).");

    private readonly ILogger<OnlineCatalogPublishingExceptionFilter> _logger;

    public OnlineCatalogPublishingExceptionFilter(ILogger<OnlineCatalogPublishingExceptionFilter> logger)
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
        UnknownCatalogChannelException => (400, "UNKNOWN_CHANNEL", "Bu satış kanalı tanımlı değil."),
        ArgumentException or BadHttpRequestException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        PostgresException or NpgsqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };
}
