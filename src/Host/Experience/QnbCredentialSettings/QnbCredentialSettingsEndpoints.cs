using System.Linq;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Invoicing.Qnb.Client;
using ALKAROS.Invoicing.Qnb.CredentialRegistration;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.QnbCredentialSettings;

/// <summary>
/// V14-QNB-006: lets a manager register QNB eSolutions e-Fatura
/// credentials (`userId`/`password` for SOAP `wsLogin`, `vergiTcKimlikNo`
/// for `belgeGonderExt`/status queries) from the interface — mirrors
/// `ALKAROS.Host.Experience.TokenTerminalSettings.TokenTerminalSettingsEndpoints`'s
/// exact auth/exception-filter shape. Does NOT call QNB's live API — see
/// the task file's Goal.
/// </summary>
public static class QnbCredentialSettingsEndpoints
{
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/qnb-credential";

    // V14-QNB-007: QNB's real published test-tenant `userService` endpoint
    // (`evidence/v0/integrations/V0-QNB-001/**`) — live-verified 2026-09-18
    // via a real `curl` call (see QnbSoapClient's own doc comment). This is
    // ONLY the login/logout surface; sending real documents is
    // `V14-QNB-001/002`'s own scope once `V0-QNB-001` closes.
    private const string QnbTestUserServiceUrl = "https://erpefaturatest1.qnbesolutions.com.tr/efatura/ws/userService";

    public static IServiceCollection AddQnbCredentialSettingsExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DualScreenStore>();
        services.TryAddSingleton<IRoleRepository, PostgresRoleRepository>();
        services.TryAddSingleton<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddSingleton<IAuthorizationService, AuthorizationService>();
        services.TryAddSingleton<ISecretProvider, EnvironmentVariableSecretProvider>();
        services.TryAddSingleton<IQnbCredentialStore, PostgresQnbCredentialStore>();
        services.TryAddTransient<QnbCredentialSettingsExceptionFilter>();
        services.AddHttpClient();
        return services;
    }

    public static RouteGroupBuilder MapQnbCredentialSettingsApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("QnbCredentialSettings")
            .RequireRateLimiting("terminal-write")
            .AddEndpointFilter<QnbCredentialSettingsExceptionFilter>();

        group.MapPost("/", async (
            Guid terminalId,
            SaveQnbCredentialHttpRequest request,
            IQnbCredentialStore credentialStore,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.IntegrationsManage, cancellationToken);

            if (string.IsNullOrWhiteSpace(request.UserId)
                || string.IsNullOrWhiteSpace(request.Password)
                || string.IsNullOrWhiteSpace(request.VergiTcKimlikNo))
            {
                return Results.BadRequest(new { error = new { code = "VALIDATION_FAILED", message = "Alanların tamamı doldurulmalıdır." } });
            }

            // V1-RMD-342 (independent 2026-09-26 audit, orta seviye bulgu): before this, any
            // non-empty string was accepted as the VKN with no format check at all, client or
            // server. The Turkish tax authority's own tax id convention (10 digits for a legal
            // entity, 11 for an individual taxpayer) is a fixed government standard, not a
            // vendor-specific guess.
            if (!IsValidVergiTcKimlikNo(request.VergiTcKimlikNo))
            {
                return Results.BadRequest(new { error = new { code = "VALIDATION_FAILED", message = "Vergi kimlik numarası 10 veya 11 haneli, yalnızca rakamlardan oluşmalıdır." } });
            }

            await credentialStore.SaveAsync(
                new SaveQnbCredentialRequest(request.UserId, request.Password, request.VergiTcKimlikNo),
                userId, cancellationToken);
            return Results.NoContent();
        });

        group.MapGet("/status", async (
            Guid terminalId,
            IQnbCredentialStore credentialStore,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.IntegrationsManage, cancellationToken);

            var status = await credentialStore.GetStatusAsync(cancellationToken);
            return Results.Ok(new QnbCredentialStatusResponse(
                status.Configured, status.UpdatedAt, status.UserId, status.VergiTcKimlikNo));
        });

        group.MapPost("/test-connection", async (
            Guid terminalId,
            IQnbCredentialStore credentialStore,
            IHttpClientFactory httpClientFactory,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            ILogger<QnbCredentialSettingsExceptionFilter> logger,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.IntegrationsManage, cancellationToken);

            var status = await credentialStore.GetStatusAsync(cancellationToken);
            var password = status.Configured ? await credentialStore.ResolvePasswordAsync(cancellationToken) : null;
            if (status.UserId is null || password is null)
                return Results.Ok(new QnbConnectionTestResponse(false, "Önce kullanıcı adı ve parola kaydedilmelidir."));

            var client = new QnbSoapClient(httpClientFactory.CreateClient(), QnbTestUserServiceUrl);
            try
            {
                await client.LoginAsync(status.UserId, password, cancellationToken: cancellationToken);
                await client.LogoutAsync(cancellationToken);
                return Results.Ok(new QnbConnectionTestResponse(true, "Bağlantı başarılı."));
            }
            catch (QnbApiException exception)
            {
                LogQnbConnectionTestFailure(logger, exception.Message, exception);
                return Results.Ok(new QnbConnectionTestResponse(false, "QNB'ye bağlanılamadı; kullanıcı adı veya parola hatalı olabilir."));
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                LogQnbConnectionTestFailure(logger, exception.Message, exception);
                return Results.Ok(new QnbConnectionTestResponse(false, "QNB sunucusuna şu anda ulaşılamıyor, daha sonra tekrar deneyin."));
            }
        });

        return group;
    }

    private static readonly Action<ILogger, string, Exception?> LogQnbConnectionTestFailure =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(5421, nameof(LogQnbConnectionTestFailure)),
            // Technical detail (the real QNB fault text or network error)
            // goes to the log only — the HTTP response above never echoes
            // it (docs/UI_STYLE_GUIDE.md §3).
            "QNB connection test failed: {Detail}");

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

    // V1-RMD-342: the Turkish tax authority's own tax id convention (10 digits for a legal
    // entity, 11 for an individual taxpayer) — both are all-digit, fixed-length national
    // identifiers, never letters or separators, so a plain digit-count check is the whole rule
    // (no checksum algorithm is published for either, unlike the bank-card Luhn check
    // elsewhere in this codebase).
    private static bool IsValidVergiTcKimlikNo(string value)
        => (value.Length == 10 || value.Length == 11) && value.All(char.IsAsciiDigit);
}

public sealed record SaveQnbCredentialHttpRequest(string UserId, string Password, string VergiTcKimlikNo);

public sealed record QnbCredentialStatusResponse(bool Configured, DateTimeOffset? UpdatedAt, string? UserId, string? VergiTcKimlikNo);

public sealed record QnbConnectionTestResponse(bool Success, string Message);

/// <summary>Mirrors TokenTerminalSettingsExceptionFilter's mapping for this settings surface.</summary>
public sealed class QnbCredentialSettingsExceptionFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5420, nameof(LogRequestFailure)),
            "QNB credential settings request failed on {Path} ({TraceIdentifier}).");

    private readonly ILogger<QnbCredentialSettingsExceptionFilter> _logger;

    public QnbCredentialSettingsExceptionFilter(ILogger<QnbCredentialSettingsExceptionFilter> logger)
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
        SensitiveDataEncryptionException => (503, "ENCRYPTION_UNAVAILABLE", "Güvenli depolama şu anda kullanılamıyor."),
        ArgumentException or BadHttpRequestException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        PostgresException or NpgsqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };
}
