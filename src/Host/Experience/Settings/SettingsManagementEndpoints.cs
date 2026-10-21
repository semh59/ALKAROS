using ALKAROS.Host.Composition.Errors;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Settings.BusinessIdentity;
using ALKAROS.Settings.GarsonFeatureToggles;
using ALKAROS.Settings.KitchenDenseModeThreshold;
using ALKAROS.Settings.KitchenLiveSync;
using ALKAROS.Settings.QrOrderExpiry;
using ALKAROS.Settings.ReservationStation;
using ALKAROS.Settings.TypedSettings;
using ALKAROS.Settings.WaiterMaxActiveTables;
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
    public const string ManagePermission = ApplicationPermissions.SettingsManage;

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
            // Settings register themselves on first read; the editor must list every one from the very first visit.
            await EnsureKnownSettingsRegisteredAsync(settings, cancellationToken);
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
            // A stale key from a shrunk palette still reads back safely; a new one must be a palette colour.
            if (key == BusinessAccentThemeSetting.Key && BusinessAccentPalette.All.All(entry => entry.Key != request.NewValue))
                throw new SettingValueRangeException(key, "Vurgu rengi hazır renklerden biri olmalı.");
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
            int? limit,
            int? offset,
            ISettingsService settings,
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            var record = await settings.GetRecordAsync(key, cancellationToken)
                ?? throw new SettingNotFoundException(key);
            var history = (await settings.GetHistoryAsync(record.SettingId, cancellationToken))
                .Skip(Math.Max(offset ?? 0, 0))
                .Take(Math.Clamp(limit ?? DefaultHistoryPageSize, 1, MaxHistoryPageSize))
                .ToList();
            var names = await DisplayNamesAsync(dataSource, history.Where(entry => entry.ChangedBy is not null).Select(entry => entry.ChangedBy!.Value), cancellationToken);
            return Results.Ok(history.Select(entry => SettingHistoryRecordV1.From(entry) with
            {
                ChangedByName = entry.ChangedBy is { } id ? names.GetValueOrDefault(id) : null,
            }).ToArray());
        });

        return group;
    }

    private const int DefaultHistoryPageSize = 100;
    private const int MaxHistoryPageSize = 500;

    private static async Task EnsureKnownSettingsRegisteredAsync(ISettingsService settings, CancellationToken cancellationToken)
    {
        await BusinessNameSetting.EnsureRegisteredAsync(settings, cancellationToken);
        await BusinessAccentThemeSetting.EnsureRegisteredAsync(settings, cancellationToken);
        await KitchenDenseModeThresholdSetting.EnsureRegisteredAsync(settings, cancellationToken);
        await KitchenLiveSyncSetting.EnsureRegisteredAsync(settings, cancellationToken);
        await QrOrderExpirySetting.EnsureRegisteredAsync(settings, cancellationToken);
        await ReservationStationSetting.EnsureRegisteredAsync(settings, cancellationToken);
        await WaiterMaxActiveTablesSetting.EnsureRegisteredAsync(settings, cancellationToken);
        await GarsonFeatureToggles.EnsureAllRegisteredAsync(settings, cancellationToken);
    }

    private static async Task<Dictionary<Guid, string>> DisplayNamesAsync(
        NpgsqlDataSource dataSource, IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToArray();
        var names = new Dictionary<Guid, string>();
        if (ids.Length == 0)
            return names;

        await using var command = dataSource.CreateCommand(
            "SELECT user_id, display_name FROM identity.users WHERE user_id = ANY(@ids);");
        command.Parameters.AddWithValue("ids", ids);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            names[reader.GetGuid(0)] = reader.GetString(1);
        return names;
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
