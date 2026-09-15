using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Settings.TypedSettings;
using ALKAROS.Settings.WaiterMaxActiveTables;
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
    private readonly IRoleRepository _roles;
    private readonly ISettingsService _settings;

    public SuggestedWaiterResolver(NpgsqlDataSource dataSource, IRoleRepository roles, ISettingsService settings)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _roles = roles ?? throw new ArgumentNullException(nameof(roles));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Among active users holding <see cref="ApplicationPermissions.OrdersSend"/>
    /// with a live (unexpired, unrevoked) device session, picks whoever
    /// currently carries the fewest non-terminal orders
    /// (<c>orders.orders.serving_user_id</c>) — the same "active load" count
    /// <c>ShiftSummaryStore</c> already computes.
    ///
    /// V1-RMD-208: when <paramref name="tableId"/> names a real table with a
    /// zone, a second-tier preference goes to whoever's own most recent open
    /// order is in that same zone (<c>table_mgmt.zones</c>) — a preference,
    /// not a filter: a candidate with no matching zone (or no zone history
    /// at all) is never excluded, only ranked behind one who has it, so a
    /// zone's very first order of the day still gets a real suggestion. Null
    /// (Cashier's own call - no real table, V1-RMD-157) skips this tier
    /// entirely, unchanged from before this existed.
    ///
    /// The final tie goes to whoever has gone longest without a new
    /// assignment (oldest <c>MAX(created_at)</c> first, nulls — never
    /// assigned — first of all), so the same person does not keep winning
    /// ties all shift. Returns null when nobody qualifies.
    ///
    /// V1-SET-006: a candidate whose own <c>active_load</c> has reached
    /// <c>waiter.max_active_tables</c> (0 = no cap, the default) drops out
    /// of the pool entirely rather than merely ranking last — a genuinely
    /// overloaded waiter must never be the answer just because everyone
    /// else happens to be busier.
    /// </summary>
    public async Task<SuggestedWaiterV1?> ResolveMostSuitableWaiterAsync(
        Guid? tableId, CancellationToken cancellationToken)
    {
        var maxActiveTables = await WaiterMaxActiveTablesSetting.GetLimitAsync(_settings, cancellationToken);

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
            LEFT JOIN LATERAL (
                SELECT t.zone_id
                FROM orders.orders o2
                JOIN table_mgmt.tables t ON t.table_id = o2.table_id
                WHERE o2.serving_user_id = u.user_id
                  AND o2.status NOT IN ('Served', 'Completed', 'Cancelled', 'Rejected')
                ORDER BY o2.created_at DESC
                LIMIT 1
            ) z ON true
            -- V1-RMD-211: reads the target table's own zone in this same
            -- query (a second round trip via a separate ResolveZoneIdAsync
            -- call used to happen before this one) — LEFT JOIN, not an
            -- inner join or a CTE joined unconditionally, so a null
            -- @table_id (Cashier, V1-RMD-157) or an id matching no row
            -- still yields exactly one (target.zone_id IS NULL) row rather
            -- than eliminating every candidate.
            LEFT JOIN (
                SELECT zone_id FROM table_mgmt.tables WHERE table_id = @table_id::uuid
            ) target ON true
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
              AND (@max_active_tables = 0 OR COALESCE(w.active_load, 0) < @max_active_tables)
            ORDER BY COALESCE(w.active_load, 0) ASC,
                     CASE
                         WHEN target.zone_id IS NOT NULL AND z.zone_id = target.zone_id THEN 0
                         ELSE 1
                     END ASC,
                     COALESCE(w.last_assigned_at, '-infinity'::timestamptz) ASC
            LIMIT 1;
            """);
        command.Parameters.Add("permission_code", NpgsqlDbType.Varchar).Value = ApplicationPermissions.OrdersSend;
        command.Parameters.Add("now", NpgsqlDbType.TimestampTz).Value = DateTimeOffset.UtcNow;
        command.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId is Guid id ? id : (object)DBNull.Value;
        command.Parameters.Add("max_active_tables", NpgsqlDbType.Integer).Value = maxActiveTables;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new SuggestedWaiterV1(reader.GetGuid(0), reader.GetString(1));
    }

    /// <summary>
    /// V1-RMD-207/210: whether <paramref name="userId"/> is a real, active
    /// user holding <see cref="ApplicationPermissions.OrdersSend"/> — the
    /// "is this genuinely a waiter" question, deliberately without the
    /// session requirement <see cref="ResolveMostSuitableWaiterAsync"/> has
    /// (that one asks "reachable right now"; this one asks "does this
    /// assignment even make sense" for a caller-supplied id that otherwise
    /// has no FK to check it, orders.orders.serving_user_id being
    /// intentionally unconstrained per V1-RMD-111's module boundary).
    ///
    /// Goes through <see cref="IRoleRepository"/> — the same canonical
    /// active/permission source every other authorization decision in this
    /// codebase reads — rather than a second, hand-written copy of that
    /// query that could silently drift from it.
    /// </summary>
    public async Task<bool> IsValidWaiterAsync(Guid userId, CancellationToken cancellationToken)
    {
        var (exists, active) = await _roles.GetUserStateAsync(userId, cancellationToken);
        if (!exists || !active) return false;

        var permissions = await _roles.GetPermissionCodesForUserAsync(userId, cancellationToken);
        return permissions.Contains(ApplicationPermissions.OrdersSend);
    }
}
