using Microsoft.AspNetCore.SignalR;

namespace ALKAROS.Host.DualScreen;

public sealed class CustomerDisplayHub : Hub
{
    public const string Route = "/hubs/customer-display";
    public const string SnapshotChanged = "SnapshotChanged";
    private readonly DualScreenStore _store;
    private readonly HubSessionRegistry _sessions;

    public CustomerDisplayHub(DualScreenStore store, HubSessionRegistry sessions)
    {
        _store = store;
        _sessions = sessions;
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var rawToken = httpContext?.Request.Cookies[DualScreenApplication.DisplayCookieName];
        var principal = await _store.AuthenticateDisplayAsync(rawToken, Context.ConnectionAborted);
        if (principal is null)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            DualScreenApplication.TerminalGroup(principal.TerminalId),
            Context.ConnectionAborted);
        _sessions.Track(Context, async cancellationToken => await _store.AuthenticateDisplayAsync(rawToken, cancellationToken) is not null);
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _sessions.Release(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
