using ALKAROS.Host.Composition.Errors;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Settings.TypedSettings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.Settings;

/// <summary>
/// V1-RMD-246: found by an independent audit (2026-09-18) —
/// ISettingsService.SetValueAsync/DeactivateAsync (V1-SET-001) existed with
/// zero HTTP surface; a setting could only ever be changed by direct
/// database access, not through the API a manager/operator would actually
/// use. Same manager-cookie pattern as Menu/Purchasing/Production management.
/// </summary>
public static class SettingsManagementEndpoints
{
    public const string ManagerCookieName = "alkaros.manager";
    public const string ManagePermission = "settings.manage";

    public static IServiceCollection AddSettingsManagementExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // PostgresSettingsRepository takes DbDataSource (the base type), not
        // NpgsqlDataSource directly — same gap every other settings-backed
        // experience registration already had to close on its own (e.g.
        // BillingSplitApplication's own ApplyTipAsync/GarsonFeature checks).
        services.TryAddSingleton<System.Data.Common.DbDataSource>(
            serviceProvider => serviceProvider.GetRequiredService<NpgsqlDataSource>());
        services.TryAddScoped<ISettingValidator, SettingValidator>();
        services.TryAddScoped<ISettingsRepository, PostgresSettingsRepository>();
        services.TryAddScoped<ISettingsService, SettingsService>();

        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        services.TryAddScoped<SettingsManagerAuthentication>();
        return services;
    }

    public static RouteGroupBuilder MapSettingsManagement(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ApiErrorHandling.EnsureFor(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/settings");
        group.AddEndpointFilter<SettingsManagerEndpointFilter>();

        group.MapGet("/", async (
            string? moduleOwner,
            ISettingsService settings,
            CancellationToken cancellationToken) =>
        {
            var records = string.IsNullOrWhiteSpace(moduleOwner)
                ? await settings.GetAllActiveAsync(cancellationToken)
                : await settings.GetByModuleOwnerAsync(moduleOwner, cancellationToken);
            return Results.Ok(records.Select(SettingRecordV1.From).ToArray());
        });

        group.MapGet("/{key}", async (
            string key,
            ISettingsService settings,
            CancellationToken cancellationToken) =>
        {
            var record = await settings.GetRecordAsync(key, cancellationToken)
                ?? throw new SettingNotFoundException(key);
            return Results.Ok(SettingRecordV1.From(record));
        });

        group.MapPut("/{key}", async (
            string key,
            UpdateSettingValueV1 request,
            ISettingsService settings,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = SettingsManagerEndpointFilter.RequireActorId(context);
            var updated = await settings.SetValueAsync(
                key, request.NewValue, request.ExpectedRowVersion, actorId, request.Reason, cancellationToken);
            return Results.Ok(SettingRecordV1.From(updated));
        });

        // DELETE cannot carry an inferred JSON body in minimal APIs (ASP.NET
        // Core rejects it for GET/HEAD/DELETE without an explicit [FromBody]);
        // no other DELETE endpoint in this codebase carries one either, so
        // this follows the same convention with query parameters instead.
        group.MapDelete("/{key}", async (
            string key,
            long expectedRowVersion,
            string? reason,
            ISettingsService settings,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = SettingsManagerEndpointFilter.RequireActorId(context);
            var deactivated = await settings.DeactivateAsync(
                key, expectedRowVersion, actorId, reason, cancellationToken);
            return Results.Ok(SettingRecordV1.From(deactivated));
        });

        group.MapGet("/{key}/history", async (
            string key,
            ISettingsService settings,
            CancellationToken cancellationToken) =>
        {
            var record = await settings.GetRecordAsync(key, cancellationToken)
                ?? throw new SettingNotFoundException(key);
            var history = await settings.GetHistoryAsync(record.SettingId, cancellationToken);
            return Results.Ok(history.Select(SettingHistoryRecordV1.From).ToArray());
        });

        return group;
    }
}

public sealed class SettingsManagerAuthentication
{
    private readonly NpgsqlDataSource _dataSource;

    public SettingsManagerAuthentication(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Guid> AuthenticateAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rawToken = context.Request.Cookies[SettingsManagementEndpoints.ManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(_dataSource, rawToken, allowSupervisor: false, cancellationToken);
        return actorId ?? throw new SettingsManagementUnauthorizedException();
    }
}

public sealed class SettingsManagerEndpointFilter : IEndpointFilter
{
    private const string ActorIdItemKey = "SettingsManagerActorId";

    private readonly SettingsManagerAuthentication _authentication;
    private readonly IAuthorizationService _authorization;

    public SettingsManagerEndpointFilter(
        SettingsManagerAuthentication authentication,
        IAuthorizationService authorization)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
    }

    public static Guid RequireActorId(HttpContext context)
        => context.Items[ActorIdItemKey] as Guid?
            ?? throw new InvalidOperationException($"{nameof(SettingsManagerEndpointFilter)} did not run before this endpoint.");

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ApiErrorScope.Enter(context.HttpContext, ApiErrorCatalog.SettingsManagement);
        var actorId = await _authentication.AuthenticateAsync(
            context.HttpContext,
            context.HttpContext.RequestAborted);
        await _authorization.AuthorizeAsync(
            actorId,
            SettingsManagementEndpoints.ManagePermission,
            context.HttpContext.RequestAborted);
        context.HttpContext.Items[ActorIdItemKey] = actorId;
        return await next(context);
    }

}

public sealed class SettingsManagementUnauthorizedException : Exception
{
    public SettingsManagementUnauthorizedException() : base("A valid settings manager session is required.")
    {
    }
}
