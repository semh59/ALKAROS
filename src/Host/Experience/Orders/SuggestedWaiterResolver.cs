using ALKAROS.Identity.Authorization.Catalog;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.Orders;

/// <summary>V1-RMD-204: the system's pick, for a caller that wants to show or use it.</summary>
public sealed record SuggestedWaiterV1(Guid UserId, string DisplayName);

/// <summary>
/// V1-RMD-202: picks who should take a new, not-yet-assigned order. First
/// used only for a QR order's pending-confirmation announcement
/// (<c>SignalRPendingOrderAnnouncer</c>); V1-RMD-204 extracts it here so
/// the same "who's most suitable right now" answer also backs an explicit
/// suggestion when a table is opened by hand.
/// </summary>
public sealed class SuggestedWaiterResolver
{
    private readonly NpgsqlDataSource _dataSource;

    public SuggestedWaiterResolver(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
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
    public async Task<SuggestedWaiterV1?> ResolveMostSuitableWaiterAsync(CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT u.user_id, u.display_name
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

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new SuggestedWaiterV1(reader.GetGuid(0), reader.GetString(1));
    }
}
