using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.QrOrdering.RelayCredential;
using ALKAROS.QrRelay.PublicGateway;
using ALKAROS.SensitiveData;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.RelaySettings;

/// <summary>
/// V12-QRT-003: lets a manager configure the relay provider's API token
/// from the interface instead of ever touching the codebase or a terminal —
/// the token is written once, encrypted at rest, and never read back.
/// </summary>
public static class RelaySettingsEndpoints
{
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/relay-credential";

    public static IServiceCollection AddRelaySettingsExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DualScreenStore>();
        services.TryAddSingleton<IRoleRepository, PostgresRoleRepository>();
        services.TryAddSingleton<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddSingleton<IAuthorizationService, AuthorizationService>();
        services.TryAddSingleton<IRelayProviderConfigStore, PostgresRelayProviderConfigStore>();
        services.TryAddSingleton<IRelayTunnelStore, PostgresRelayTunnelStore>();
        services.AddHttpClient<ICloudflareApiClient, CloudflareApiClient>();
        services.TryAddScoped<IRelayProvisioningService, RelayProvisioningService>();
        services.TryAddTransient<RelaySettingsExceptionFilter>();
        return services;
    }

    public static RouteGroupBuilder MapRelaySettingsApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("RelaySettings")
            .RequireRateLimiting("terminal-write")
            .AddEndpointFilter<RelaySettingsExceptionFilter>();

        group.MapPost("/", async (
            Guid terminalId,
            SaveRelayCredentialRequest request,
            IRelayCredentialStore credentialStore,
            IRelayProviderConfigStore configStore,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.IntegrationsManage, cancellationToken);

            if (string.IsNullOrWhiteSpace(request.CloudflareApiToken)
                || string.IsNullOrWhiteSpace(request.AccountId)
                || string.IsNullOrWhiteSpace(request.ZoneId)
                || string.IsNullOrWhiteSpace(request.BaseDomain))
            {
                return Results.BadRequest(new { error = new { code = "VALIDATION_FAILED", message = "Alanların tamamı doldurulmalıdır." } });
            }

            await credentialStore.SaveCloudflareApiTokenAsync(request.CloudflareApiToken, userId, cancellationToken);
            await configStore.SaveAsync(request.AccountId, request.ZoneId, request.BaseDomain, cancellationToken);
            return Results.NoContent();
        });

        group.MapGet("/status", async (
            Guid terminalId,
            IRelayCredentialStore credentialStore,
            IRelayProviderConfigStore configStore,
            IRelayTunnelStore tunnelStore,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.IntegrationsManage, cancellationToken);

            var status = await credentialStore.GetStatusAsync(cancellationToken);
            var config = await configStore.GetAsync(cancellationToken);
            var tunnel = await tunnelStore.GetInfoAsync(cancellationToken);
            return Results.Ok(new RelayCredentialStatusResponse(
                status.Configured, status.UpdatedAt, config?.AccountId, config?.ZoneId, config?.BaseDomain,
                tunnel?.Hostname, tunnel?.UpdatedAt));
        });

        group.MapPost("/provision", async (
            Guid terminalId,
            ProvisionRelayTunnelRequest request,
            IRelayProvisioningService provisioningService,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.IntegrationsManage, cancellationToken);

            if (string.IsNullOrWhiteSpace(request.SubdomainLabel))
            {
                return Results.BadRequest(new { error = new { code = "VALIDATION_FAILED", message = "Alt alan adı etiketi doldurulmalıdır." } });
            }

            var result = await provisioningService.ProvisionAsync(request.SubdomainLabel, cancellationToken);
            return Results.Ok(new ProvisionRelayTunnelResponse(result.Hostname));
        });

        return group;
    }

    private static async Task<Guid> RequireCashierPermissionAsync(
        HttpContext context,
        Guid terminalId,
        DualScreenStore store,
        IAuthorizationService authorization,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        var cashierToken = context.Request.Cookies[DualScreenApplication.CashierCookieName];
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

        await authorization.AuthorizeAsync(principal.UserId, permissionCode, cancellationToken);
        return principal.UserId;
    }
}

/// <summary>Mirrors OrderManagementExceptionFilter's mapping (V1-ORD-005 pattern) for this settings surface.</summary>
public sealed class RelaySettingsExceptionFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5400, nameof(LogRequestFailure)),
            "Relay settings request failed on {Path} ({TraceIdentifier}).");

    private readonly ILogger<RelaySettingsExceptionFilter> _logger;

    public RelaySettingsExceptionFilter(ILogger<RelaySettingsExceptionFilter> logger)
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
        DualScreenUnauthorizedException => (401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
        AuthorizationDeniedException => (403, "FORBIDDEN", "Bu işlem için yetkiniz yok."),
        RelayProvisioningException provisioning => (422, "PROVISIONING_FAILED", provisioning.Message),
        SensitiveDataEncryptionException => (503, "ENCRYPTION_UNAVAILABLE", "Güvenli depolama şu anda kullanılamıyor."),
        ArgumentException or BadHttpRequestException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        PostgresException or NpgsqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };
}
