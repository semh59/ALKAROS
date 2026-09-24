using ALKAROS.Audit.EventStore;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Security.IdentityHardening;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.SecurityAdministration;

/// <summary>
/// V1-RMD-266: the security administration surface. AccountRecoveryService
/// (V15-SEC-002) was built, tested and DI-registered but no caller existed, so
/// nobody could sign a user out everywhere or clear a lockout. Every route here
/// requires a MANAGER session (never supervisor) holding security.manage, the
/// manager-exclusive grant added by migration 142. Later administration
/// operations mount on this same group and filter.
/// </summary>
public static class SecurityAdministrationEndpoints
{
    public const string ManagerCookieName = "alkaros.manager";
    public const string ManagePermission = ApplicationPermissions.SecurityManage;

    public static IServiceCollection AddSecurityAdministrationExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        services.TryAddSingleton<IAuditEventStore, PostgresAuditEventStore>();
        // Replaces the in-memory reference sink of the Security module: a
        // recovery action that vanishes on restart is not an audit trail.
        services.AddSingleton<ISuspiciousLoginAuditSink, AuditEventStoreSuspiciousLoginSink>();
        services.TryAddScoped<SecurityAdministrationAuthentication>();
        return services;
    }

    public static RouteGroupBuilder MapSecurityAdministrationApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/security");
        group.AddEndpointFilter<SecurityAdministrationEndpointFilter>();

        group.MapPost("/users/{userId:guid}/revoke-sessions", async (
            Guid userId,
            AccountRecoveryService recovery,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = SecurityAdministrationEndpointFilter.RequireActorId(context);
            var revoked = await recovery.RevokeAllSessionsAsync(userId, actorId.ToString("D"), cancellationToken);
            return Results.Ok(new RevokeSessionsResultV1(userId, revoked));
        });

        group.MapPost("/users/{userId:guid}/force-unlock", async (
            Guid userId,
            AccountRecoveryService recovery,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = SecurityAdministrationEndpointFilter.RequireActorId(context);
            var unlocked = await recovery.ForceUnlockAsync(userId, actorId.ToString("D"), cancellationToken);
            return unlocked
                ? Results.Ok(new ForceUnlockResultV1(userId, true))
                : Results.Json(
                    new SecurityAdministrationApiErrorEnvelopeV1(new SecurityAdministrationApiErrorV1(
                        "NOT_FOUND", "İstenen kullanıcı bulunamadı.", StatusCodes.Status404NotFound, context.TraceIdentifier)),
                    statusCode: StatusCodes.Status404NotFound);
        });

        return group;
    }
}

public sealed record RevokeSessionsResultV1(Guid UserId, int RevokedSessions);

public sealed record ForceUnlockResultV1(Guid UserId, bool Unlocked);

public sealed record SecurityAdministrationApiErrorV1(string Code, string Message, int Status, string TraceId);

public sealed record SecurityAdministrationApiErrorEnvelopeV1(SecurityAdministrationApiErrorV1 Error);

public sealed class SecurityAdministrationUnauthorizedException : Exception
{
    public SecurityAdministrationUnauthorizedException() : base("A valid manager session is required.")
    {
    }
}

public sealed class SecurityAdministrationAuthentication
{
    private readonly NpgsqlDataSource _dataSource;

    public SecurityAdministrationAuthentication(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Guid> AuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rawToken = context.Request.Cookies[SecurityAdministrationEndpoints.ManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(_dataSource, rawToken, allowSupervisor: false, cancellationToken);
        return actorId ?? throw new SecurityAdministrationUnauthorizedException();
    }
}

public sealed class SecurityAdministrationEndpointFilter : IEndpointFilter
{
    private const string ActorIdItemKey = "SecurityAdministrationActorId";

    private readonly SecurityAdministrationAuthentication _authentication;
    private readonly IAuthorizationService _authorization;

    public SecurityAdministrationEndpointFilter(
        SecurityAdministrationAuthentication authentication, IAuthorizationService authorization)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
    }

    public static Guid RequireActorId(HttpContext context)
        => context.Items[ActorIdItemKey] as Guid?
            ?? throw new InvalidOperationException($"{nameof(SecurityAdministrationEndpointFilter)} did not run before this endpoint.");

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            var actorId = await _authentication.AuthenticateAsync(context.HttpContext, context.HttpContext.RequestAborted);
            await _authorization.AuthorizeAsync(actorId, SecurityAdministrationEndpoints.ManagePermission, context.HttpContext.RequestAborted);
            context.HttpContext.Items[ActorIdItemKey] = actorId;
            return await next(context);
        }
        catch (SecurityAdministrationUnauthorizedException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (AuthorizationDeniedException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (PostgresException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (NpgsqlException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (ArgumentException exception)
        {
            return MapError(context.HttpContext, exception);
        }
        catch (BadHttpRequestException exception)
        {
            return MapError(context.HttpContext, exception);
        }
    }

    private static IResult MapError(HttpContext context, Exception exception)
    {
        var (status, code, message) = exception switch
        {
            SecurityAdministrationUnauthorizedException =>
                (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            AuthorizationDeniedException =>
                (StatusCodes.Status403Forbidden, "FORBIDDEN", "Güvenlik yönetimi için yeterli izin yok."),
            ArgumentException or BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            NpgsqlException =>
                (StatusCodes.Status503ServiceUnavailable, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => throw exception,
        };
        return Results.Json(
            new SecurityAdministrationApiErrorEnvelopeV1(new SecurityAdministrationApiErrorV1(code, message, status, context.TraceIdentifier)),
            statusCode: status);
    }
}
