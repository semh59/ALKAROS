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

    /// <summary>V1-RMD-287: the till's queue of checks awaiting payment changed; the till reloads it.</summary>
    public const string PendingChecksChanged = "PendingChecksChanged";

    /// <summary>The SignalR group a given waiter's connections join.</summary>
    public static string GroupName(Guid userId) => $"waiter-user:{userId:D}";

    /// <summary>
    /// V1-RMD-149: a guest-entered order is waiting for staff confirmation.
    /// The hub used to carry only <see cref="OrderItemReady"/>, so a QR order
    /// reached PendingConfirmation with nobody told.
    /// </summary>
    public const string OrderPendingConfirmation = "OrderPendingConfirmation";

    private static readonly object ConnectedUserIdItemKey = new();

    private readonly DualScreenStore _store;
    private readonly WaiterPresenceTracker _presence;

    public WaiterOrderStatusHub(DualScreenStore store, WaiterPresenceTracker presence)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _presence = presence ?? throw new ArgumentNullException(nameof(presence));
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
        // V1-RMD-203: remembered here so OnDisconnectedAsync (which carries
        // no principal of its own) knows whose count to decrement.
        Context.Items[ConnectedUserIdItemKey] = principal.UserId;
        _presence.Connected(principal.UserId);
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(ConnectedUserIdItemKey, out var value) && value is Guid userId)
            _presence.Disconnected(userId);
        return base.OnDisconnectedAsync(exception);
    }
}

/// <summary>Payload for <see cref="WaiterOrderStatusHub.OrderItemReady"/>.</summary>
public sealed record OrderItemReadyV1(Guid OrderId, Guid? TableId, Guid OrderItemId, string ProductName);

/// <summary>Payload for <see cref="WaiterOrderStatusHub.PendingChecksChanged"/>; <c>Change</c> is <c>Sent</c> or <c>Recalled</c>.</summary>
public sealed record PendingChecksChangedV1(string Change, Guid OrderId, Guid TableId);
