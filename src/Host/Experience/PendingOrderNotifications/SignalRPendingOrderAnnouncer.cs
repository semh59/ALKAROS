using ALKAROS.Host.Experience.WaiterNotifications;
using ALKAROS.Host.Experience.WebPush;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Orders.Integration;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.PendingOrderNotifications;

/// <summary>
/// V1-RMD-149: the Host end of <see cref="IPendingOrderAnnouncer"/> — pushes
/// the announcement over <see cref="WaiterOrderStatusHub"/>.
///
/// V1-RMD-202: a brand-new QR order has no <c>ServingUserId</c> yet (nobody
/// has taken it), so unlike the "item is ready" path (V1-RMD-201, which
/// targets the order's own serving waiter) this one has to pick a
/// <see cref="ResolveMostSuitableWaiterAsync">candidate</see>: whoever holds
/// <c>orders.send</c>, has an open session, and currently carries the
/// fewest open orders (ties broken by whoever has gone longest without a
/// new one — a fairness/rotation rule, not a second load metric). When no
/// candidate exists (nobody is logged in) this falls back to the original
/// flat broadcast, so an announcement is never silently dropped.
///
/// V1-WTR-011: and over Web Push, for a device whose app is closed. The two
/// channels are deliberately both used rather than one chosen: SignalR is
/// instant but only reaches an open app, while a push survives a locked
/// screen. A device that receives both shows one notification — the service
/// worker and the in-page handler share the tag "alkaros-pending-order".
/// </summary>
public sealed class SignalRPendingOrderAnnouncer : IPendingOrderAnnouncer
{
    private readonly IHubContext<WaiterOrderStatusHub> _hub;
    private readonly WaiterPresenceTracker _presence;
    private readonly NpgsqlDataSource _dataSource;
    private readonly WebPushSender? _push;

    public SignalRPendingOrderAnnouncer(
        IHubContext<WaiterOrderStatusHub> hub,
        WaiterPresenceTracker presence,
        NpgsqlDataSource dataSource,
        WebPushSender? push = null)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _push = push;
    }

    public async Task AnnounceAsync(PendingOrderAnnouncement announcement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        var waiterId = await ResolveMostSuitableWaiterAsync(cancellationToken);

        // V1-RMD-203: the resolved candidate only holds a live
        // identity.device_sessions row, which Cashier/PosTerminal sessions
        // also satisfy — neither ever connects to this hub, so a candidate
        // must additionally be a live hub connection before being targeted.
        var recipients = waiterId is Guid id && _presence.IsConnected(id)
            ? _hub.Clients.Group(WaiterOrderStatusHub.GroupName(id))
            : _hub.Clients.All;
        await recipients.SendAsync(
            WaiterOrderStatusHub.OrderPendingConfirmation, announcement, cancellationToken);

        if (_push is null) return;

        // Same wording as the in-app banner: what happened at the table, not
        // which channel it arrived through (docs/UI_STYLE_GUIDE.md).
        var message = new WebPushMessage(
            "Misafir siparişi",
            $"{announcement.TableNumber} masası sipariş verdi — {announcement.ItemCount} kalem",
            "alkaros-pending-order");
        if (waiterId is Guid pushId && await _push.HasAnySubscriptionAsync(pushId, cancellationToken))
            await _push.SendToUserAsync(message, pushId, cancellationToken);
        else
            await _push.BroadcastAsync(message, cancellationToken);
    }

    /// <summary>
    /// Among active users holding <see cref="ApplicationPermissions.OrdersSend"/>
    /// with a live (unexpired, unrevoked) device session, picks whoever
    /// currently carries the fewest non-terminal orders
    /// (<c>orders.orders.serving_user_id</c>) — the same "active load" count
    /// <c>ShiftSummaryStore</c> already computes. A tie goes to whoever has
    /// gone longest without a new assignment (oldest <c>MAX(created_at)</c>
    /// first, nulls — never assigned — first of all), so the same person
    /// does not keep winning ties all shift. Returns null when nobody
    /// qualifies.
    /// </summary>
    private async Task<Guid?> ResolveMostSuitableWaiterAsync(CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT u.user_id
            FROM identity.users u
            LEFT JOIN LATERAL (
                SELECT
                    COUNT(*) FILTER (
                        WHERE o.status NOT IN ('Served', 'Completed', 'Cancelled', 'Rejected')
                    ) AS active_load,
                    MAX(o.created_at) AS last_assigned_at
                FROM orders.orders o
                WHERE o.serving_user_id = u.user_id
            ) w ON true
            WHERE u.active
              AND EXISTS (
                  SELECT 1
                  FROM identity.user_roles ur
                  JOIN identity.role_permissions rp ON rp.role_id = ur.role_id
                  JOIN identity.permissions p ON p.permission_id = rp.permission_id
                  WHERE ur.user_id = u.user_id AND p.code = @permission_code
              )
              AND EXISTS (
                  SELECT 1
                  FROM identity.device_sessions s
                  WHERE s.user_id = u.user_id
                    AND s.revoked_at IS NULL
                    AND s.expires_at > @now
              )
            ORDER BY COALESCE(w.active_load, 0) ASC,
                     COALESCE(w.last_assigned_at, '-infinity'::timestamptz) ASC
            LIMIT 1;
            """);
        command.Parameters.Add("permission_code", NpgsqlDbType.Varchar).Value = ApplicationPermissions.OrdersSend;
        command.Parameters.Add("now", NpgsqlDbType.TimestampTz).Value = DateTimeOffset.UtcNow;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid userId ? userId : null;
    }
}
