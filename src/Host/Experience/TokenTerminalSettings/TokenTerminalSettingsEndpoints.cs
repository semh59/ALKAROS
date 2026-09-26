using System.Linq;
using ALKAROS.Host.DualScreen;
using ALKAROS.Host.Experience.RelaySettings;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Payments.Token.TerminalCredential;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.TokenTerminalSettings;

/// <summary>
/// V13-HUG-005: lets a manager register the Token/Beko terminal's
/// `merchant-id`/`branch-id`/`terminal-id` (printed on the physical fiscal device or
/// its app's QR code) and `client-id`/`client-secret` from the interface —
/// mirrors `ALKAROS.Host.Experience.RelaySettings.RelaySettingsEndpoints`'s
/// exact auth/exception-filter shape for `V12-QRT-003`'s Cloudflare token.
/// Does NOT call Token's live API — see the task file's Goal.
/// </summary>
public static class TokenTerminalSettingsEndpoints
{
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/token-credential";

    public static IServiceCollection AddTokenTerminalSettingsExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DualScreenStore>();
        services.TryAddSingleton<IRoleRepository, PostgresRoleRepository>();
        services.TryAddSingleton<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddSingleton<IAuthorizationService, AuthorizationService>();
        services.TryAddSingleton<ISecretProvider, EnvironmentVariableSecretProvider>();
        services.TryAddSingleton<ITokenTerminalCredentialStore, PostgresTokenTerminalCredentialStore>();
        services.TryAddTransient<TokenTerminalSettingsExceptionFilter>();
        return services;
    }

    public static RouteGroupBuilder MapTokenTerminalSettingsApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("TokenTerminalSettings")
            .RequireRateLimiting("terminal-write")
            .AddEndpointFilter<TokenTerminalSettingsExceptionFilter>();

        group.MapPost("/", async (
            Guid terminalId,
            SaveTokenTerminalCredentialHttpRequest request,
            ITokenTerminalCredentialStore credentialStore,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.IntegrationsManage, cancellationToken);

            if (string.IsNullOrWhiteSpace(request.MerchantId)
                || string.IsNullOrWhiteSpace(request.BranchId)
                || string.IsNullOrWhiteSpace(request.TerminalId)
                || string.IsNullOrWhiteSpace(request.ClientId)
                || string.IsNullOrWhiteSpace(request.ClientSecret))
            {
                return Results.BadRequest(new { error = new { code = "VALIDATION_FAILED", message = "Alanların tamamı doldurulmalıdır." } });
            }

            // V1-RMD-342 (independent 2026-09-26 audit, orta seviye bulgu): before this, any
            // non-empty string was accepted as the terminal id with no format check - the
            // screen's own label already promises this shape (an "AV"/"AT" prefix) to the
            // manager typing it in, this just actually enforces the promise. MerchantId/
            // BranchId have no similarly confirmed format in this codebase's own research
            // (evidence/v0/integrations/V0-HUG-001/) to validate against.
            if (!IsValidTokenTerminalId(request.TerminalId))
            {
                return Results.BadRequest(new { error = new { code = "VALIDATION_FAILED", message = "Terminal kimliği 'AV' veya 'AT' ile başlamalı ve ardından yalnızca rakam içermelidir." } });
            }

            await credentialStore.SaveAsync(
                new SaveTokenTerminalCredentialRequest(
                    request.MerchantId, request.BranchId, request.TerminalId, request.ClientId, request.ClientSecret),
                userId, cancellationToken);
            return Results.NoContent();
        });

        group.MapGet("/status", async (
            Guid terminalId,
            ITokenTerminalCredentialStore credentialStore,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, dualStore, authorization, ApplicationPermissions.IntegrationsManage, cancellationToken);

            var status = await credentialStore.GetStatusAsync(cancellationToken);
            return Results.Ok(new TokenTerminalCredentialStatusResponse(
                status.Configured, status.UpdatedAt, status.MerchantId, status.BranchId, status.TerminalId, status.ClientId));
        });

        return group;
    }

    // Identical to RelaySettingsEndpoints' own private helper — both settings
    // surfaces gate the same way (cashier cookie or Bearer token, then
    // `integrations.manage`), and neither owns the other's file, so this is
    // duplicated rather than extracted into a shared file across two
    // separately-owned Owned surfaces.
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

    // V1-RMD-342: matches this screen's own UI hint/placeholder ("AV0000111044") — two-letter
    // prefix ("AV" or "AT"), then digits only, case-insensitive since the physical label's own
    // casing is not something a manager reliably retypes exactly.
    private static bool IsValidTokenTerminalId(string value)
    {
        if (value.Length < 3)
            return false;
        var prefix = value[..2];
        if (!prefix.Equals("AV", StringComparison.OrdinalIgnoreCase) && !prefix.Equals("AT", StringComparison.OrdinalIgnoreCase))
            return false;
        return value[2..].All(char.IsAsciiDigit);
    }
}

public sealed record SaveTokenTerminalCredentialHttpRequest(
    string MerchantId, string BranchId, string TerminalId, string ClientId, string ClientSecret);

public sealed record TokenTerminalCredentialStatusResponse(
    bool Configured, DateTimeOffset? UpdatedAt, string? MerchantId, string? BranchId, string? TerminalId, string? ClientId);

/// <summary>Mirrors RelaySettingsExceptionFilter's mapping for this settings surface.</summary>
public sealed class TokenTerminalSettingsExceptionFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5410, nameof(LogRequestFailure)),
            "Token terminal settings request failed on {Path} ({TraceIdentifier}).");

    private readonly ILogger<TokenTerminalSettingsExceptionFilter> _logger;

    public TokenTerminalSettingsExceptionFilter(ILogger<TokenTerminalSettingsExceptionFilter> logger)
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
