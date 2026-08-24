using Microsoft.AspNetCore.SignalR;

namespace ALKAROS.Host.DualScreen;

public sealed class CustomerDisplayHub : Hub
{
    public const string Route = "/hubs/customer-display";
    public const string SnapshotChanged = "SnapshotChanged";
    private readonly DualScreenStore _store;

    public CustomerDisplayHub(DualScreenStore store)
    {
        _store = store;
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var rawToken = httpContext?.Request.Cookies[DualScreenApplication.DisplayCookieName];
        var principal = await _store.AuthenticateDisplayAsync(rawToken, null, Context.ConnectionAborted);
        if (principal is null)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            DualScreenApplication.TerminalGroup(principal.TerminalId),
            Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }
}
