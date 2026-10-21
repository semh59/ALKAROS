using ALKAROS.Audit.EventStore;
using ALKAROS.Host.Composition.Errors;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Identity.Authorization.Policies;
using ALKAROS.Host.Experience;
using ALKAROS.Host.Experience.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        services.TryAddScoped<IAuthorizationPolicyRepository, PostgresAuthorizationPolicyRepository>();
        services.TryAddScoped<RoleManagementAuthentication>();
        services.TryAddSingleton<IAuditEventStore, PostgresAuditEventStore>();
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
            IAuditEventStore audit,
            CancellationToken cancellationToken) =>
        {
            await roles.AddPermissionAsync(ActorId(http), request.Code, request.Name, cancellationToken);
            await AppendAuditAsync(audit, http, "identity.permission-added", "Permission", PermissionAggregateId(request.Code),
                new { code = request.Code, name = request.Name }, cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/roles", async (
            CreateRoleRequestV1 request,
            HttpContext http,
            IRoleManagementService roles,
            IAuditEventStore audit,
            CancellationToken cancellationToken) =>
        {
            var roleId = await roles.CreateRoleAsync(ActorId(http), request.Code, request.Name, cancellationToken);
            await AppendAuditAsync(audit, http, "identity.role-created", "Role", roleId,
                new { code = request.Code, name = request.Name }, cancellationToken);
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
            IAuditEventStore audit,
            CancellationToken cancellationToken) =>
        {
            await roles.AssignPermissionAsync(ActorId(http), roleId, request.PermissionCode, cancellationToken);
            await AppendAuditAsync(audit, http, "identity.role-permission-assigned", "Role", roleId,
                new { permissionCode = request.PermissionCode }, cancellationToken);
            return Results.NoContent();
        });

        group.MapDelete("/roles/{roleId:guid}/permissions/{permissionCode}", async (
            Guid roleId,
            string permissionCode,
            HttpContext http,
            IRoleManagementService roles,
            IAuditEventStore audit,
            CancellationToken cancellationToken) =>
        {
            await roles.RevokePermissionAsync(ActorId(http), roleId, permissionCode, cancellationToken);
            await AppendAuditAsync(audit, http, "identity.role-permission-revoked", "Role", roleId,
                new { permissionCode }, cancellationToken);
            return Results.NoContent();
        });

        group.MapPost("/roles/{roleId:guid}/users/{userId:guid}", async (
            Guid roleId,
            Guid userId,
            HttpContext http,
            IRoleManagementService roles,
            IAuditEventStore audit,
            CancellationToken cancellationToken) =>
        {
            await roles.AssignUserAsync(ActorId(http), userId, roleId, cancellationToken);
            await AppendAuditAsync(audit, http, "identity.user-role-assigned", "User", userId,
                new { roleId }, cancellationToken);
            return Results.NoContent();
        });

        group.MapDelete("/roles/{roleId:guid}/users/{userId:guid}", async (
            Guid roleId,
            Guid userId,
            HttpContext http,
            IRoleManagementService roles,
            IAuditEventStore audit,
            CancellationToken cancellationToken) =>
        {
            await roles.RevokeUserAsync(ActorId(http), userId, roleId, cancellationToken);
            await AppendAuditAsync(audit, http, "identity.user-role-revoked", "User", userId,
                new { roleId }, cancellationToken);
            return Results.NoContent();
        });

        // The limits a role may approve on its own (docs/domain/authorization-model.md). No row for a pair means a
        // manager decides. Only the three grant-class permissions and the two roles that hold them through a
        // request are editable, and only as auto_within; always_allow is never reachable from here.
        group.MapGet("/policies", async (
            HttpContext http,
            IAuthorizationService authorization,
            IAuthorizationPolicyRepository policies,
            CancellationToken cancellationToken) =>
        {
            await authorization.AuthorizeAsync(ActorId(http), PermissionCodes.RolesManage, cancellationToken);
            var listing = await policies.ListAsync(cancellationToken);
            return Results.Ok(listing.Where(IsEditablePolicy).Select(ToPolicyV1).ToArray());
        });

        group.MapPut("/policies/{permissionCode}/{roleCode}", async (
            string permissionCode,
            string roleCode,
            SetPolicyRequestV1 request,
            HttpContext http,
            IAuthorizationService authorization,
            IAuthorizationPolicyRepository policies,
            IAuditEventStore audit,
            CancellationToken cancellationToken) =>
        {
            var actor = ActorId(http);
            await authorization.AuthorizeAsync(actor, PermissionCodes.RolesManage, cancellationToken);
            request.Validate(permissionCode, roleCode);

            var existing = await policies.GetAsync(permissionCode, roleCode, cancellationToken);
            var versionMatches = existing is null ? request.ExpectedRowVersion is null : existing.RowVersion == request.ExpectedRowVersion;
            if (!versionMatches)
                throw new AuthorizationPolicyConcurrencyException(permissionCode, roleCode);

            AuthorizationPolicy saved;
            try
            {
                saved = await policies.UpsertAsync(
                    new AuthorizationPolicy(
                        Guid.Empty, permissionCode, roleCode, PolicyMode.AutoWithin,
                        request.LimitAmount, request.MaxCount, request.WindowSeconds, 0),
                    request.ExpectedRowVersion, actor, cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new AuthorizationPolicyConcurrencyException(permissionCode, roleCode);
            }

            await AppendAuditAsync(audit, http, "identity.policy-changed", "AuthorizationPolicy", saved.PolicyId,
                new
                {
                    permissionCode,
                    roleCode,
                    before = existing is null ? null : new { existing.LimitAmount, existing.MaxCount, existing.WindowSeconds },
                    after = new { saved.LimitAmount, saved.MaxCount, saved.WindowSeconds },
                }, cancellationToken);
            return Results.Ok(ToPolicyV1(saved));
        });

        group.MapDelete("/policies/{permissionCode}/{roleCode}", async (
            string permissionCode,
            string roleCode,
            [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
            HttpContext http,
            IAuthorizationService authorization,
            IAuthorizationPolicyRepository policies,
            IAuditEventStore audit,
            CancellationToken cancellationToken) =>
        {
            var actor = ActorId(http);
            await authorization.AuthorizeAsync(actor, PermissionCodes.RolesManage, cancellationToken);
            SetPolicyRequestV1.ValidateScope(permissionCode, roleCode);

            var existing = await policies.GetAsync(permissionCode, roleCode, cancellationToken);
            if (existing is not null && await policies.DeleteAsync(permissionCode, roleCode, cancellationToken))
            {
                await AppendAuditAsync(audit, http, "identity.policy-removed", "AuthorizationPolicy", existing.PolicyId,
                    new { permissionCode, roleCode, before = new { existing.LimitAmount, existing.MaxCount, existing.WindowSeconds } },
                    cancellationToken);
            }

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
            IAuditEventStore audit,
            CancellationToken cancellationToken) =>
        {
            var userId = await roles.CreateUserAsync(
                ActorId(http), request.Username, request.Password, request.DisplayName, cancellationToken);
            await AppendAuditAsync(audit, http, "identity.user-created", "User", userId,
                new { username = request.Username.Trim(), displayName = request.DisplayName }, cancellationToken);
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

    private static bool IsEditablePolicy(AuthorizationPolicy policy)
        => policy.Mode == PolicyMode.AutoWithin
            && SetPolicyRequestV1.EditablePermissions.Contains(policy.PermissionCode)
            && SetPolicyRequestV1.EditableRoles.Contains(policy.RoleCode);

    private static PolicyV1 ToPolicyV1(AuthorizationPolicy policy)
        => new(policy.PermissionCode, policy.RoleCode, policy.LimitAmount!.Value, policy.MaxCount!.Value,
            policy.WindowSeconds!.Value, policy.RowVersion);

    // The audit log needs an aggregate id and the command does not return one for a permission, so the code stands in for it.
    private static Guid PermissionAggregateId(string code)
        => new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(code))[..16]);

    // Written after the command succeeded, in the same request. The two stores cannot share one transaction; a failed
    // write surfaces as an error to the caller rather than being swallowed.
    private static Task AppendAuditAsync(
        IAuditEventStore audit, HttpContext http, string eventName, string aggregateType, Guid aggregateId,
        object after, CancellationToken cancellationToken)
        => audit.AppendAsync(
            new AuditEvent(
                id: Guid.NewGuid(),
                eventName: eventName,
                aggregateType: aggregateType,
                aggregateId: aggregateId,
                actorType: "User",
                correlationId: http.TraceIdentifier,
                actorId: ActorId(http),
                afterStateJson: System.Text.Json.JsonSerializer.Serialize(after)),
            cancellationToken);

    internal const string ActorItemKey = "alkaros.role-management.actor";

    private static Guid ActorId(HttpContext http)
        => http.Items[ActorItemKey] is Guid actor
            ? actor
            : throw new RoleManagementUnauthorizedException();
}

public sealed record AddPermissionRequestV1(string Code, string Name);

public sealed record PolicyV1(
    string PermissionCode, string RoleCode, decimal LimitAmount, int MaxCount, int WindowSeconds, long RowVersion);

public sealed record SetPolicyRequestV1(
    string IdempotencyKey, decimal LimitAmount, int MaxCount, int WindowSeconds, long? ExpectedRowVersion)
{
    public const decimal MaximumLimit = 1000m;
    public const int MaximumCount = 100;
    public const int MinimumWindowSeconds = 3600;
    public const int MaximumWindowSeconds = 86400;

    public static readonly IReadOnlySet<string> EditablePermissions = new HashSet<string>(StringComparer.Ordinal)
    {
        ApplicationPermissions.BillsVoid, ApplicationPermissions.BillsComp, ApplicationPermissions.BillsDiscount,
    };

    public static readonly IReadOnlySet<string> EditableRoles = new HashSet<string>(StringComparer.Ordinal)
    {
        ApplicationPermissions.RoleWaiter, ApplicationPermissions.RoleCashier,
    };

    public static void ValidateScope(string permissionCode, string roleCode)
    {
        if (!EditablePermissions.Contains(permissionCode) || !EditableRoles.Contains(roleCode))
            throw new InvalidPolicyException();
    }

    public void Validate(string permissionCode, string roleCode)
    {
        ValidateScope(permissionCode, roleCode);
        if (LimitAmount is <= 0m or > MaximumLimit
            || MaxCount is < 1 or > MaximumCount
            || WindowSeconds is < MinimumWindowSeconds or > MaximumWindowSeconds)
        {
            throw new InvalidPolicyException();
        }
    }
}

/// <summary>The limit, count or window is outside what the screen may set, or the permission/role is not editable.</summary>
public sealed class InvalidPolicyException : Exception
{
    public InvalidPolicyException()
        : base("The policy scope or its limits are outside the allowed range.")
    {
    }
}

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
