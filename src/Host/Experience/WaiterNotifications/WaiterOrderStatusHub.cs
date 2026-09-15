using ALKAROS.Host.DualScreen;
using Microsoft.AspNetCore.SignalR;

namespace ALKAROS.Host.Experience.WaiterNotifications;

/// <summary>
/// V1-WTR-009: pushes "an order item is ready" to connected waiter devices —
/// only reachable when a deployment has turned on kitchen live-sync
/// (V1-SET-002). Same shape as
/// <see cref="ALKAROS.Host.DualScreen.CustomerDisplayHub"/>. Every
/// authenticated connection joins its own per-user group (see
/// <see cref="GroupName"/>) on connect; V1-RMD-201 uses this so a
/// notification for an order with a known <c>ServingUserId</c> reaches only
/// that waiter's devices instead of every connected device.
/// </summary>
public sealed class WaiterOrderStatusHub : Hub
{
    public const string Route = "/hubs/waiter-order-status";
    public const string OrderItemReady = "OrderItemReady";

    /// <summary>The SignalR group a given waiter's connections join.</summary>
    public static string GroupName(Guid userId) => $"waiter-user:{userId:D}";

    /// <summary>
    /// V1-RMD-149: a guest-entered order is waiting for staff confirmation.
    /// The hub used to carry only <see cref="OrderItemReady"/>, so a QR order
    /// reached PendingConfirmation with nobody told.
    /// </summary>
    public const string OrderPendingConfirmation = "OrderPendingConfirmation";

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

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(principal.UserId), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }
}

/// <summary>Payload for <see cref="WaiterOrderStatusHub.OrderItemReady"/>.</summary>
public sealed record OrderItemReadyV1(Guid OrderId, Guid? TableId, Guid OrderItemId, string ProductName);
