using ALKAROS.Host.DualScreen;
using Microsoft.AspNetCore.SignalR;

namespace ALKAROS.Host.Experience.WaiterNotifications;

/// <summary>
/// V1-WTR-009: pushes "an order item is ready" to every connected waiter
/// device — only reachable when a deployment has turned on kitchen
/// live-sync (V1-SET-002). Same shape as
/// <see cref="ALKAROS.Host.DualScreen.CustomerDisplayHub"/>, but there is no
/// waiter-to-table (or waiter-to-order) assignment tracked anywhere in this
/// system to target one specific device, so this is a flat broadcast to
/// every authenticated connection on this hub rather than a per-table or
/// per-waiter group; the payload carries the table and item so a client can
/// decide what is relevant to show. Targeted delivery is a natural follow-on
/// once/if a waiter-table assignment model exists.
/// </summary>
public sealed class WaiterOrderStatusHub : Hub
{
    public const string Route = "/hubs/waiter-order-status";
    public const string OrderItemReady = "OrderItemReady";

    private readonly DualScreenStore _store;

    public WaiterOrderStatusHub(DualScreenStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var rawToken = httpContext?.Request.Cookies[DualScreenApplication.CashierCookieName];
        var terminalIdText = httpContext?.Request.Query["terminalId"].ToString();
        if (string.IsNullOrEmpty(rawToken)
            || string.IsNullOrEmpty(terminalIdText)
            || !Guid.TryParse(terminalIdText, out var terminalId))
        {
            Context.Abort();
            return;
        }

        var principal = await _store.AuthenticateCashierAsync(rawToken, terminalId, Context.ConnectionAborted);
        if (principal is null)
        {
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }
}

/// <summary>Payload for <see cref="WaiterOrderStatusHub.OrderItemReady"/>.</summary>
public sealed record OrderItemReadyV1(Guid OrderId, Guid? TableId, Guid OrderItemId, string ProductName);
