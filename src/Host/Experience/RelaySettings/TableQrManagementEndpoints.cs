using ALKAROS.Host.Composition.Errors;
using ALKAROS.Host.Experience.Catalog;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.QrOrdering.TokenLifecycle;
using ALKAROS.QrRelay.PublicGateway;
using ALKAROS.Tables.TableLifecycle;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.RelaySettings;

/// <summary>
/// A manager issues, rotates and revokes each table's printed QR code. Deliberately not under <c>/api/v1/qr</c>:
/// that prefix is served on the public relay origin, this surface is staff-only. The raw token is not stored, so
/// it is returned exactly once, inside the QR address, by issue and rotate; the list never carries it.
/// </summary>
public static class TableQrManagementEndpoints
{
    public const string RoutePrefix = "/api/v1/management/qr-tables";

    // The cashier's quick-sale anchor row (migration 098) is not a seat; it never gets a QR.
    private static readonly Guid QuickSaleAnchorTableId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static IServiceCollection AddTableQrManagementExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ITableTokenRepository, PostgresTableTokenRepository>();
        services.TryAddSingleton<TableTokenService>();
        services.TryAddSingleton<ITableRepository, PostgresTableRepository>();
        services.TryAddSingleton<IZoneRepository, PostgresZoneRepository>();
        services.TryAddSingleton<IRelayTunnelStore, PostgresRelayTunnelStore>();
        services.TryAddSingleton<IRoleRepository, PostgresRoleRepository>();
        services.TryAddSingleton<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddSingleton<IAuthorizationService, AuthorizationService>();
        return services;
    }

    public static RouteGroupBuilder MapTableQrManagementApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ApiErrorHandling.EnsureFor(endpoints);
        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("TableQrManagement")
            .AddEndpointFilter<TableQrManagerEndpointFilter>();

        group.MapGet("/", async (
            ITableRepository tables,
            IZoneRepository zones,
            ITableTokenRepository tokens,
            CancellationToken cancellationToken) =>
        {
            var now = DateTimeOffset.UtcNow;
            var issuedAtByTable = (await tokens.ListActiveAsync(cancellationToken))
                .Where(token => !token.IsExpired(now))
                .ToDictionary(token => token.TableId, token => token.IssuedAt);

            var rows = new List<TableQrRowV1>();
            foreach (var zone in (await zones.GetAllAsync(cancellationToken)).OrderBy(zone => zone.SortOrder).ThenBy(zone => zone.Name, StringComparer.CurrentCulture))
            {
                foreach (var table in await tables.GetByZoneAsync(zone.Id, cancellationToken))
                    rows.Add(ToRow(table, zone.Name, issuedAtByTable));
            }

            foreach (var table in await tables.GetUnzonedAsync(cancellationToken))
            {
                if (table.Id != QuickSaleAnchorTableId)
                    rows.Add(ToRow(table, null, issuedAtByTable));
            }

            return Results.Ok(rows);
        });

        group.MapPost("/{tableId:guid}/issue", async (
            Guid tableId,
            TableTokenService tokenService,
            ITableRepository tables,
            IRelayTunnelStore tunnel,
            CancellationToken cancellationToken) =>
        {
            var hostname = await RequireIssuableAsync(tableId, tables, tunnel, cancellationToken);
            return Results.Ok(ToCode(tableId, hostname, await tokenService.IssueAsync(tableId, cancellationToken)));
        });

        group.MapPost("/{tableId:guid}/rotate", async (
            Guid tableId,
            TableTokenService tokenService,
            ITableRepository tables,
            IRelayTunnelStore tunnel,
            CancellationToken cancellationToken) =>
        {
            var hostname = await RequireIssuableAsync(tableId, tables, tunnel, cancellationToken);
            return Results.Ok(ToCode(tableId, hostname, await tokenService.RotateAsync(tableId, cancellationToken)));
        });

        group.MapPost("/{tableId:guid}/revoke", async (
            Guid tableId,
            TableTokenService tokenService,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var actorId = TableQrManagerEndpointFilter.RequireActorId(context);
            await tokenService.RevokeAsync(tableId, $"manager:{actorId:D}", cancellationToken);
            return Results.NoContent();
        });

        return group;
    }

    private static TableQrRowV1 ToRow(Table table, string? zoneName, Dictionary<Guid, DateTimeOffset> issuedAtByTable) =>
        new(table.Id, table.TableNumber, zoneName, table.Active, issuedAtByTable.TryGetValue(table.Id, out var issuedAt) ? issuedAt : null);

    // Checked before a token exists, so an unusable QR (no table, no relay address) is never issued.
    private static async Task<string> RequireIssuableAsync(
        Guid tableId, ITableRepository tables, IRelayTunnelStore tunnel, CancellationToken cancellationToken)
    {
        if (tableId == QuickSaleAnchorTableId || await tables.GetByIdAsync(tableId, cancellationToken) is null)
            throw new TableQrTableNotFoundException();

        return (await tunnel.GetInfoAsync(cancellationToken))?.Hostname
            ?? throw new TableQrRelayNotReadyException();
    }

    private static TableQrCodeV1 ToCode(Guid tableId, string hostname, string rawToken) =>
        new(tableId, $"https://{hostname}/qr/?t={Uri.EscapeDataString(rawToken)}");
}

/// <summary><c>QrIssuedAt</c> is null when the table has no usable QR; the raw token is never part of a row.</summary>
public sealed record TableQrRowV1(Guid TableId, string TableNumber, string? ZoneName, bool TableActive, DateTimeOffset? QrIssuedAt);

/// <summary>The address to encode into the printed QR. Shown once: the server keeps only a hash of the token.</summary>
public sealed record TableQrCodeV1(Guid TableId, string QrUrl);

public sealed class TableQrManagerEndpointFilter : IEndpointFilter
{
    private const string ActorIdItemKey = "TableQrManagerActorId";

    private readonly NpgsqlDataSource _dataSource;
    private readonly IAuthorizationService _authorization;

    public TableQrManagerEndpointFilter(NpgsqlDataSource dataSource, IAuthorizationService authorization)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
    }

    public static Guid RequireActorId(HttpContext context)
        => context.Items[ActorIdItemKey] as Guid?
            ?? throw new InvalidOperationException($"{nameof(TableQrManagerEndpointFilter)} did not run before this endpoint.");

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        ApiErrorScope.Enter(http, ApiErrorCatalog.TableQrManagement);
        var rawToken = http.Request.Cookies[CatalogManagementEndpoints.ManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(_dataSource, rawToken, allowSupervisor: false, http.RequestAborted)
            ?? throw new TableQrUnauthorizedException();
        await _authorization.AuthorizeAsync(actorId, ApplicationPermissions.IntegrationsManage, http.RequestAborted);
        http.Items[ActorIdItemKey] = actorId;
        return await next(context);
    }
}

public sealed class TableQrUnauthorizedException : Exception
{
    public TableQrUnauthorizedException() : base("A valid manager session is required.") { }
}

public sealed class TableQrTableNotFoundException : Exception
{
    public TableQrTableNotFoundException() : base("The table does not exist.") { }
}

public sealed class TableQrRelayNotReadyException : Exception
{
    public TableQrRelayNotReadyException() : base("The relay tunnel has not been provisioned, so a QR address cannot be built.") { }
}
