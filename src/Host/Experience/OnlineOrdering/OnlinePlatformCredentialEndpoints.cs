using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.OnlineOrdering.Credentials;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.OnlineOrdering;

/// <summary>
/// V12-OUI-003: a manager (<c>integrations.manage</c>) reads and changes each online platform's API settings.
/// Secret fields only ever come back as "set / not set". Same cashier-session auth and error shape as the QNB
/// credential settings surface. Nothing here calls a platform (that is each platform adapter's own scope).
/// </summary>
public static class OnlinePlatformCredentialEndpoints
{
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/online-platform-credentials";

    public static IServiceCollection AddOnlinePlatformCredentialExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DualScreenStore>();
        services.TryAddSingleton<IRoleRepository, PostgresRoleRepository>();
        services.TryAddSingleton<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddSingleton<IAuthorizationService, AuthorizationService>();
        // TryAdd defers to OnlineOrderingModule in the real Host.
        services.TryAddTransient<ISecretProvider, EnvironmentVariableSecretProvider>();
        services.TryAddTransient<IOnlinePlatformCredentialStore, PostgresOnlinePlatformCredentialStore>();
        services.TryAddTransient<OnlinePlatformCredentialExceptionFilter>();
        return services;
    }

    public static RouteGroupBuilder MapOnlinePlatformCredentialApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("OnlinePlatformCredentials")
            .RequireRateLimiting("terminal-write")
            .AddEndpointFilter<OnlinePlatformCredentialExceptionFilter>();

        group.MapGet("/", async (
            Guid terminalId,
            IOnlinePlatformCredentialStore store,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireManagerAsync(context, terminalId, dualStore, authorization, cancellationToken);
            var platforms = new List<OnlinePlatformCredentialStatusV1>();
            foreach (var platform in OnlinePlatformCredentialCatalog.Platforms)
                platforms.Add(ToV1(await store.GetStatusAsync(platform.Provider, cancellationToken)));
            return Results.Ok(new OnlinePlatformCredentialsV1(platforms));
        });

        group.MapPut("/{provider}", async (
            Guid terminalId,
            string provider,
            SaveOnlinePlatformCredentialRequestV1 request,
            IOnlinePlatformCredentialStore store,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = await RequireManagerAsync(context, terminalId, dualStore, authorization, cancellationToken);
            var status = await store.SaveAsync(
                new SaveOnlinePlatformCredentialRequest(
                    provider,
                    request.Values ?? new Dictionary<string, string>(),
                    request.Cleared ?? []),
                userId,
                cancellationToken);
            return Results.Ok(ToV1(status));
        });

        return group;
    }

    private static OnlinePlatformCredentialStatusV1 ToV1(OnlinePlatformCredentialStatus status) => new(
        status.Provider,
        status.Fields.Select(field => new OnlinePlatformCredentialFieldV1(field.Name, field.IsSecret, field.Configured, field.Value)).ToList(),
        status.UpdatedAt);

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

public sealed record SaveOnlinePlatformCredentialRequestV1(Dictionary<string, string>? Values, string[]? Cleared);

public sealed record OnlinePlatformCredentialFieldV1(string Name, bool IsSecret, bool Configured, string? Value);

public sealed record OnlinePlatformCredentialStatusV1(
    string Provider,
    IReadOnlyList<OnlinePlatformCredentialFieldV1> Fields,
    DateTimeOffset? UpdatedAt);

public sealed record OnlinePlatformCredentialsV1(IReadOnlyList<OnlinePlatformCredentialStatusV1> Platforms);

/// <summary>Maps failures to Turkish messages (docs/UI_STYLE_GUIDE.md §3); a refused field is named by its id.</summary>
public sealed class OnlinePlatformCredentialExceptionFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5430, nameof(LogRequestFailure)),
            "Online platform credential request failed on {Path} ({TraceIdentifier}).");

    private readonly ILogger<OnlinePlatformCredentialExceptionFilter> _logger;

    public OnlinePlatformCredentialExceptionFilter(ILogger<OnlinePlatformCredentialExceptionFilter> logger)
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

            return Results.Json(
                new { error = new { code = mapped.Code, message = mapped.Message, field = (exception as InvalidOnlinePlatformCredentialException)?.Field } },
                statusCode: mapped.Status);
        }
    }

    public static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        DualScreenUnauthorizedException => (401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
        AuthorizationDeniedException => (403, "FORBIDDEN", "Bu işlem için yetkiniz yok."),
        UnknownOnlinePlatformException => (404, "PLATFORM_NOT_FOUND", "Bu online platform tanınmıyor."),
        InvalidOnlinePlatformCredentialException invalid => (400, "VALIDATION_FAILED", Message(invalid.Problem)),
        SensitiveDataEncryptionException => (503, "ENCRYPTION_UNAVAILABLE", "Güvenli depolama şu anda kullanılamıyor."),
        ArgumentException or BadHttpRequestException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        PostgresException or NpgsqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };

    private static string Message(OnlinePlatformCredentialProblem problem) => problem switch
    {
        OnlinePlatformCredentialProblem.NoChange => "Kaydedilecek bir değişiklik yok.",
        OnlinePlatformCredentialProblem.UnknownField => "Bu platformda böyle bir alan yok.",
        OnlinePlatformCredentialProblem.SetAndCleared => "Aynı alan hem girilip hem silinemez.",
        OnlinePlatformCredentialProblem.Empty => "Alan boş bırakılamaz; silmek için Kaldır'ı kullanın.",
        OnlinePlatformCredentialProblem.TooLong => "Değer çok uzun.",
        OnlinePlatformCredentialProblem.InvalidCharacters => "Değer geçersiz karakter içeriyor.",
        OnlinePlatformCredentialProblem.InvalidUrl => "Adres https:// ile başlayan geçerli bir adres olmalıdır.",
        _ => "İstek doğrulanamadı.",
    };
}
