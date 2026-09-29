using ALKAROS.Host.Composition.Errors;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Behavioural;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Identity.Authorization.Delegations;
using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Host.Experience;
using ALKAROS.Host.Experience.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.Authorization;

/// <summary>
/// The manager / supervisor decision surface (V1-IAM-020). Reads the pending
/// authorization grants, active delegations and open behavioural tightenings,
/// and resolves them. Mounted under <c>/api/v1/management/authorization</c> and
/// gated on a manager or supervisor device session plus <c>reports.view</c>
/// (held outright by both roles, model §3).
/// </summary>
public static class AuthorizationDecisionEndpoints
{
    public const string GroupPrefix = "/api/v1/management/authorization";

    /// <summary>The floor-supervisor / manager marker permission both roles hold.</summary>
    public const string DecisionPermission = ApplicationPermissions.ReportsView;

    public static IServiceCollection AddAuthorizationDecisionExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IAuthorizationGrantRepository, PostgresAuthorizationGrantRepository>();
        services.TryAddScoped<IAuthorizationDelegationRepository, PostgresAuthorizationDelegationRepository>();
        services.TryAddScoped<IBehaviouralTighteningRepository, PostgresBehaviouralTighteningRepository>();
        services.TryAddScoped<IBehaviouralTighteningService, BehaviouralTighteningService>();
        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        services.TryAddScoped<AuthorizationDecisionAuthentication>();
        services.TryAddScoped<AuthorizationDecisionStore>();
        return services;
    }

    public static RouteGroupBuilder MapAuthorizationDecisionApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ApiErrorHandling.EnsureFor(endpoints);
        var group = endpoints.MapGroup(GroupPrefix);
        group.AddEndpointFilter<AuthorizationDecisionEndpointFilter>();

        group.MapGet("/pending-grants", async (
            AuthorizationDecisionStore store, CancellationToken cancellationToken) =>
            Results.Ok(await store.ListPendingGrantsAsync(cancellationToken)));

        group.MapPost("/grants/{grantId:guid}/approve", async (
            Guid grantId, HttpContext http, AuthorizationDecisionStore store, CancellationToken cancellationToken) =>
            Results.Ok(await store.ApproveAsync(grantId, ActorId(http), cancellationToken)));

        group.MapPost("/grants/{grantId:guid}/deny", async (
            Guid grantId, HttpContext http, AuthorizationDecisionStore store, CancellationToken cancellationToken) =>
            Results.Ok(await store.DenyAsync(grantId, ActorId(http), cancellationToken)));

        group.MapGet("/delegations", async (
            AuthorizationDecisionStore store, CancellationToken cancellationToken) =>
            Results.Ok(await store.ListActiveDelegationsAsync(cancellationToken)));

        // V1-RMD-407 (V1-RMD-399 H-04): nothing could create a delegation, so the time-boxed hand-off
        // (model §1, V1-IAM-021) only ever existed on rows inserted by hand.
        group.MapPost("/delegations", async (
            CreateDelegationRequestV1 request,
            HttpContext http,
            AuthorizationDecisionStore store,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            var actorId = ActorId(http);
            if (!AuthorizationDecisionStore.IsDelegable(request.PermissionCode))
                throw new ArgumentException("Only bills.void, bills.comp and bills.discount can be delegated.", nameof(request));
            try
            {
                await authorization.AuthorizeAsync(actorId, request.PermissionCode!, cancellationToken);
            }
            catch (AuthorizationDeniedException)
            {
                throw new DelegatorLacksPermissionException(request.PermissionCode!);
            }

            var created = await store.CreateDelegationAsync(request, actorId, cancellationToken);
            return Results.Created($"{GroupPrefix}/delegations/{created.DelegationId:D}", created);
        });

        group.MapPost("/delegations/{delegationId:guid}/revoke", async (
            Guid delegationId, HttpContext http, AuthorizationDecisionStore store, CancellationToken cancellationToken) =>
        {
            var revoked = await store.RevokeDelegationAsync(delegationId, ActorId(http), cancellationToken);
            return revoked ? Results.NoContent() : Results.NotFound();
        });

        group.MapGet("/behavioural-tightenings", async (
            AuthorizationDecisionStore store, CancellationToken cancellationToken) =>
            Results.Ok(await store.ListOpenTighteningsAsync(cancellationToken)));

        group.MapPost("/behavioural-tightenings/{tighteningId:guid}/clear", async (
            Guid tighteningId, HttpContext http, AuthorizationDecisionStore store, CancellationToken cancellationToken) =>
        {
            await store.ClearTighteningAsync(tighteningId, ActorId(http), cancellationToken);
            return Results.NoContent();
        });

        return group;
    }

    internal const string ActorItemKey = "alkaros.authorization-decision.actor";

    private static Guid ActorId(HttpContext http)
        => http.Items[ActorItemKey] is Guid actor
            ? actor
            : throw new AuthorizationDecisionUnauthorizedException();
}

public sealed class AuthorizationDecisionAuthentication
{
    private readonly NpgsqlDataSource _dataSource;

    public AuthorizationDecisionAuthentication(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Guid> AuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rawToken = context.Request.Cookies[CatalogManagementEndpoints.ManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(_dataSource, rawToken, allowSupervisor: true, cancellationToken);
        return actorId ?? throw new AuthorizationDecisionUnauthorizedException();
    }
}

public sealed class AuthorizationDecisionEndpointFilter : IEndpointFilter
{
    private readonly AuthorizationDecisionAuthentication _authentication;
    private readonly IAuthorizationService _authorization;

    public AuthorizationDecisionEndpointFilter(
        AuthorizationDecisionAuthentication authentication, IAuthorizationService authorization)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        ApiErrorScope.Enter(context.HttpContext, ApiErrorCatalog.AuthorizationDecisions);
        var actorId = await _authentication.AuthenticateAsync(http, http.RequestAborted);
        await _authorization.AuthorizeAsync(
            actorId, AuthorizationDecisionEndpoints.DecisionPermission, http.RequestAborted);
        http.Items[AuthorizationDecisionEndpoints.ActorItemKey] = actorId;
        return await next(context);
    }

}

public sealed class AuthorizationDecisionUnauthorizedException : Exception
{
    public AuthorizationDecisionUnauthorizedException()
        : base("A valid manager or supervisor session is required.")
    {
    }
}
