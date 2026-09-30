using ALKAROS.Host.Composition.Errors;
using ALKAROS.Identity.Authorization;
using ALKAROS.Host.Experience;
using ALKAROS.Host.Experience.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.Roles;

/// <summary>
/// HTTP surface for <see cref="IRoleManagementService"/> (found inert by an
/// independent audit, 2026-09-05: fully implemented and registered in
/// IdentityModule, but role/permission administration could previously only
/// be done via raw SQL). Mounted under <c>/api/v1/management/roles</c> and
/// gated on a manager or supervisor device session; each command then makes
/// its own <c>identity.roles.manage</c> / <c>identity.permissions.manage</c>
/// authorization decision (CODE-008 linearization), so the endpoint filter
/// here only authenticates — it does not itself gate on a single permission.
///
/// Found while designing waiter-table ownership tracking (2026-09-06,
/// V1-RMD-110): this entire surface was reachable by session but denied by
/// permission for every actor, including the bootstrap manager —
/// `identity.users.manage`/`identity.roles.manage`/etc. (migration 008) were
/// never granted to any role anywhere (migration 054 fixes this). Combined
/// with there being no way at all to create a second user account
/// (provision-manager refuses once any user exists), a real deployment could
/// never actually reach any command here.
/// </summary>
public static class RoleManagementEndpoints
{
    public const string GroupPrefix = "/api/v1/management/roles";

    public static IServiceCollection AddRoleManagementExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IPermissionRepository, PostgresPermissionRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        services.TryAddScoped<IRoleManagementService, RoleManagementService>();
        services.TryAddScoped<RoleManagementAuthentication>();
        return services;
    }

    public static RouteGroupBuilder MapRoleManagementApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ApiErrorHandling.EnsureFor(endpoints);
        var group = endpoints.MapGroup(GroupPrefix);
        group.AddEndpointFilter<RoleManagementEndpointFilter>();

        group.MapPost("/permissions", async (
            AddPermissionRequestV1 request,
            HttpContext http,
            IRoleManagementService roles,
            CancellationToken cancellationToken) =>
        {
            await roles.AddPermissionAsync(ActorId(http), request.Code, request.Name, cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/roles", async (
            CreateRoleRequestV1 request,
            HttpContext http,
            IRoleManagementService roles,
            CancellationToken cancellationToken) =>
        {
            var roleId = await roles.CreateRoleAsync(ActorId(http), request.Code, request.Name, cancellationToken);
            return Results.Ok(new CreateRoleResultV1(roleId));
        });

        group.MapGet("/roles", async (
            HttpContext http,
            IRoleManagementService roles,
            CancellationToken cancellationToken) =>
        {
            var listing = await roles.ListRolesAsync(ActorId(http), cancellationToken);
            return Results.Ok(listing.Select(role => new RoleV1(role.Id, role.Code, role.Name, role.PermissionCodes)).ToArray());
        });

        group.MapGet("/permissions", async (
            HttpContext http,
            IRoleManagementService roles,
            CancellationToken cancellationToken) =>
        {
            var listing = await roles.ListPermissionsAsync(ActorId(http), cancellationToken);
            return Results.Ok(listing.Select(permission => new PermissionV1(permission.Code, permission.Name)).ToArray());
        });

        group.MapPost("/roles/{roleId:guid}/permissions", async (
            Guid roleId,
            AssignPermissionRequestV1 request,
            HttpContext http,
            IRoleManagementService roles,
            CancellationToken cancellationToken) =>
        {
            await roles.AssignPermissionAsync(ActorId(http), roleId, request.PermissionCode, cancellationToken);
            return Results.NoContent();
        });

        group.MapDelete("/roles/{roleId:guid}/permissions/{permissionCode}", async (
            Guid roleId,
            string permissionCode,
            HttpContext http,
            IRoleManagementService roles,
            CancellationToken cancellationToken) =>
        {
            await roles.RevokePermissionAsync(ActorId(http), roleId, permissionCode, cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/roles/{roleId:guid}/users/{userId:guid}", async (
            Guid roleId,
            Guid userId,
            HttpContext http,
            IRoleManagementService roles,
            CancellationToken cancellationToken) =>
        {
            await roles.AssignUserAsync(ActorId(http), userId, roleId, cancellationToken);
            return Results.NoContent();
        });

        group.MapDelete("/roles/{roleId:guid}/users/{userId:guid}", async (
            Guid roleId,
            Guid userId,
            HttpContext http,
            IRoleManagementService roles,
            CancellationToken cancellationToken) =>
        {
            await roles.RevokeUserAsync(ActorId(http), userId, roleId, cancellationToken);
            return Results.NoContent();
        });

        // V1-RMD-110: found while designing waiter-table ownership tracking —
        // there was no way, anywhere in the system, to create a second user
        // account. provision-manager refuses to run once any user exists, and
        // this whole role-management surface was itself unreachable in
        // practice (see AddRoleManagementExperience's identity.users.manage /
        // identity.roles.manage grant note). Creates the account only; the
        // caller assigns a role separately via the existing
        // POST /roles/{roleId}/users/{userId}.
        var usersGroup = endpoints.MapGroup("/api/v1/management/users");
        usersGroup.AddEndpointFilter<RoleManagementEndpointFilter>();
        usersGroup.MapPost("", async (
            CreateUserRequestV1 request,
            HttpContext http,
            IRoleManagementService roles,
            CancellationToken cancellationToken) =>
        {
            var userId = await roles.CreateUserAsync(
                ActorId(http), request.Username, request.Password, request.DisplayName, cancellationToken);
            return Results.Ok(new CreateUserResultV1(userId));
        });

        usersGroup.MapGet("", async (
            HttpContext http,
            IRoleManagementService roles,
            CancellationToken cancellationToken) =>
        {
            var listing = await roles.ListUsersAsync(ActorId(http), cancellationToken);
            return Results.Ok(listing.Select(user => new UserV1(user.UserId, user.Username, user.DisplayName, user.Active, user.RoleIds)).ToArray());
        });

        return group;
    }

    internal const string ActorItemKey = "alkaros.role-management.actor";

    private static Guid ActorId(HttpContext http)
        => http.Items[ActorItemKey] is Guid actor
            ? actor
            : throw new RoleManagementUnauthorizedException();
}

public sealed record AddPermissionRequestV1(string Code, string Name);

public sealed record CreateRoleRequestV1(string Code, string Name);

public sealed record AssignPermissionRequestV1(string PermissionCode);

public sealed record CreateUserRequestV1(string Username, string Password, string DisplayName);

public sealed record CreateUserResultV1(Guid UserId);

public sealed record CreateRoleResultV1(Guid RoleId);

public sealed record RoleV1(Guid RoleId, string Code, string Name, IReadOnlyList<string> PermissionCodes);

public sealed record PermissionV1(string Code, string Name);

public sealed record UserV1(Guid UserId, string Username, string DisplayName, bool Active, IReadOnlyList<Guid> RoleIds);

public sealed class RoleManagementAuthentication
{
    private readonly NpgsqlDataSource _dataSource;

    public RoleManagementAuthentication(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Guid> AuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rawToken = context.Request.Cookies[CatalogManagementEndpoints.ManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(_dataSource, rawToken, allowSupervisor: true, cancellationToken);
        return actorId ?? throw new RoleManagementUnauthorizedException();
    }
}

public sealed class RoleManagementEndpointFilter : IEndpointFilter
{
    private readonly RoleManagementAuthentication _authentication;

    public RoleManagementEndpointFilter(RoleManagementAuthentication authentication)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        ApiErrorScope.Enter(context.HttpContext, ApiErrorCatalog.RoleManagement);
        var actorId = await _authentication.AuthenticateAsync(http, http.RequestAborted);
        http.Items[RoleManagementEndpoints.ActorItemKey] = actorId;
        return await next(context);
    }

}

public sealed class RoleManagementUnauthorizedException : Exception
{
    public RoleManagementUnauthorizedException()
        : base("A valid manager or supervisor session is required.")
    {
    }
}
