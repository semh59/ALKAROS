using ALKAROS.Host.DualScreen;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace ALKAROS.Host.Experience.HelpRequests;

/// <summary>
/// V1-WTR-014: pushes a waiter's "help needed at table X" call to every
/// connected manager/supervisor session — a channel that did not exist at
/// all before this (a waiter's only option was walking over in person).
/// Auth is deliberately the MANAGEMENT cookie (`alkaros.manager`,
/// <see cref="ManagementSessionLookup"/>), not the cashier one
/// <see cref="ALKAROS.Host.Experience.WaiterNotifications.WaiterOrderStatusHub"/>
/// uses — the recipients here are manager/supervisor sessions specifically
/// (Semih's decision, 2026-09-11: cashier is excluded, they are busy at the
/// till), and that cookie is only ever set on a manager/supervisor login
/// (DualScreenApplication's login endpoint deletes it for every other
/// role). Same flat-broadcast shape as WaiterOrderStatusHub for the same
/// reason: there is no manager-to-terminal assignment tracked anywhere to
/// target one specific device.
/// </summary>
public sealed class HelpRequestHub : Hub
{
    public const string Route = "/hubs/help-requests";
    public const string HelpRequested = "HelpRequested";

    private readonly NpgsqlDataSource _dataSource;

    public HelpRequestHub(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var rawToken = httpContext?.Request.Cookies[CatalogManagementEndpointsManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(
            _dataSource, rawToken, allowSupervisor: true, Context.ConnectionAborted);
        if (actorId is null)
        {
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }

    // Every Experience area that authenticates against the management
    // cookie defines its own copy of the name (V1-RMD-121's own doc comment
    // on ManagementSessionLookup: duplicated by design, not a boundary
    // violation — each reads identity's own schema for its own check).
    private const string CatalogManagementEndpointsManagerCookieName = "alkaros.manager";
}
