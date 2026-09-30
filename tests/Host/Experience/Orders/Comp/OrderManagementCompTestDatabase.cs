using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.TestHelpers;

namespace ALKAROS.Host.Experience.Orders.Comp.Tests;

/// <summary>
/// Isolated database for `POST .../orders/{orderId}/items/{itemId}/comp`
/// (V1-BIL-005): users/identity/authorization/catalog/audit/tables/orders,
/// plus the full grant-flow schema (policies, grants, delegations,
/// behavioural tightenings) so the test can prove the policy engine,
/// DelegationEscalationResolver and BehaviouralTighteningGate all actually
/// run on this path, not just that the endpoint exists.
/// </summary>
public sealed class OrderManagementCompTestDatabase : PgTestDatabase
{
    public OrderManagementCompTestDatabase()
        : base("alkaros_bil005_")
    {
    }

    protected override async Task ApplySqlAsync()
    {
        var sqlDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql");
        foreach (var file in Directory.GetFiles(sqlDirectory, "*.up.sql").OrderBy(f => f))
            await RunAsync(DataSource, await File.ReadAllTextAsync(file));
    }

    /// <summary>Seeds a cashier device session for a fresh user under the given role, with the given permission codes granted outright.</summary>
    public async Task<(Guid UserId, string Cookie)> SeedCashierSessionAsync(
        Guid terminalId, string roleCode, params string[] permissionCodes)
    {
        var userId = Guid.NewGuid();
        var suffix = userId.ToString("N");
        var (raw, hash) = DeviceSessionToken.Create();

        await ExecuteAsync(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Comp API Test', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            """,
            ("user_id", userId),
            ("username", "bil005-api-" + suffix),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"cashier:{terminalId:D}"),
            ("token_hash", hash));

        var roleId = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO identity.roles (role_id, code, name) VALUES (@role_id, @role_code, 'Comp API Test Role');",
            ("role_id", roleId),
            ("role_code", roleCode + "-" + suffix));
        await ExecuteAsync(
            "INSERT INTO identity.user_roles (user_role_id, user_id, role_id) VALUES (@id, @user_id, @role_id);",
            ("id", Guid.NewGuid()),
            ("user_id", userId),
            ("role_id", roleId));

        foreach (var code in permissionCodes)
        {
            await ExecuteAsync(
                """
                INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
                SELECT @id, @role_id, permission_id FROM identity.permissions WHERE code = @code;
                """,
                ("id", Guid.NewGuid()),
                ("role_id", roleId),
                ("code", code));
        }

        return (userId, $"{DualScreenApplication.CashierCookieName}={raw}");
    }

    /// <summary>
    /// V1-RMD-111: assigns the REAL, canonically-coded 'waiter' role seeded
    /// by migration 043 — not a per-test throwaway role with a uniquifying
    /// suffix like <see cref="SeedCashierSessionAsync"/> above. The own-check
    /// guard in AuthorizationGrantService keys off the literal role code
    /// "waiter" (ApplicationPermissions.RoleWaiter), so an own-check
    /// regression test needs the genuine row.
    /// </summary>
    public async Task<(Guid UserId, string Cookie)> SeedRealWaiterSessionAsync(Guid terminalId)
    {
        var userId = Guid.NewGuid();
        var suffix = userId.ToString("N");
        var (raw, hash) = DeviceSessionToken.Create();

        await ExecuteAsync(
            """
            INSERT INTO identity.users (user_id, username, password_hash, display_name, active)
            VALUES (@user_id, @username, 'not-used', 'Comp API Test Waiter', true);
            INSERT INTO identity.device_sessions (session_id, user_id, device_id, token_hash, created_at, expires_at)
            VALUES (@session_id, @user_id, @device_id, @token_hash, now(), now() + interval '1 hour');
            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            SELECT @user_role_id, @user_id, role_id FROM identity.roles WHERE code = 'waiter';
            """,
            ("user_id", userId),
            ("username", "bil005-waiter-" + suffix),
            ("session_id", Guid.NewGuid()),
            ("device_id", $"cashier:{terminalId:D}"),
            ("token_hash", hash),
            ("user_role_id", Guid.NewGuid()));

        return (userId, $"{DualScreenApplication.CashierCookieName}={raw}");
    }

    /// <summary>V1-RMD-408: also assigns the migration-seeded <paramref name="roleCode"/> role to <paramref name="userId"/>.</summary>
    public Task AddSeededRoleAsync(Guid userId, string roleCode)
        => ExecuteAsync(
            """
            INSERT INTO identity.user_roles (user_role_id, user_id, role_id)
            SELECT @user_role_id, @user_id, role_id FROM identity.roles WHERE code = @role_code;
            """,
            ("user_role_id", Guid.NewGuid()),
            ("user_id", userId),
            ("role_code", roleCode));

    /// <summary>
    /// V1-RMD-408: re-inserts the user's <paramref name="roleCode"/> assignment so it is physically the user's last
    /// role row — the pre-fix code picked whichever row the database returned first.
    /// </summary>
    public async Task MoveRoleAssignmentLastAsync(Guid userId, string roleCode)
    {
        await ExecuteAsync(
            """
            DELETE FROM identity.user_roles
            WHERE user_id = @user_id AND role_id = (SELECT role_id FROM identity.roles WHERE code = @role_code);
            """,
            ("user_id", userId),
            ("role_code", roleCode));
        await AddSeededRoleAsync(userId, roleCode);
    }

    /// <summary>Seeds a catalog product and an Active order with a single Active item (unit price 100, tax-inclusive 10% -> gross 100).</summary>
    public Task<(Guid OrderId, Guid ItemId)> SeedActiveOrderWithOneItemAsync()
        => SeedActiveOrderWithOneItemAsync(servingUserId: null);

    /// <summary>
    /// V1-RMD-111: same seed, but the order is attributed to
    /// <paramref name="servingUserId"/> — needed to exercise the own-check
    /// guard, which reads Order.ServingUserId end to end from here through
    /// the comp endpoint.
    /// </summary>
    public Task<(Guid OrderId, Guid ItemId)> SeedActiveOrderWithOneItemAsync(Guid? servingUserId)
        => SeedActiveOrderWithOneItemAsync(servingUserId, unitPrice: 100m, taxRate: 10m);

    /// <summary>
    /// V1-WTR-012: lets a test control the item's exact GrossAmount
    /// (taxRate 0 makes GrossAmount == unitPrice, no rounding surprises) —
    /// needed to place an item precisely inside or outside
    /// PersonalCompBudgetPolicy's per-item/daily caps.
    /// </summary>
    public async Task<(Guid OrderId, Guid ItemId)> SeedActiveOrderWithOneItemAsync(
        Guid? servingUserId, decimal unitPrice, decimal taxRate)
    {
        var productId = Guid.NewGuid();
        await ExecuteAsync(
            """
            INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active)
            VALUES (@product_id, @sku, 'Comp Test Product', 1, 1, true);
            """,
            ("product_id", productId),
            ("sku", "bil005-" + productId.ToString("N")[..8]));

        var orderItem = new OrderItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            productId,
            "Comp Test Product",
            quantity: 1,
            unitPrice: unitPrice,
            taxRate: taxRate,
            status: OrderItemState.Active);

        var order = new Order(
            Guid.NewGuid(),
            OrderSource.Cashier,
            "BIL-005-" + orderItem.Id.ToString("N")[..8],
            new[] { orderItem },
            status: OrderState.Submitted,
            servingUserId: servingUserId);

        var repository = new PostgresOrderRepository(DataSource);
        await repository.AddAsync(order);

        return (order.Id, orderItem.Id);
    }

    public Task SeedPolicyAsync(string permissionCode, string roleCode, string mode, decimal? limitAmount = null, int? maxCount = null, int? windowSeconds = null)
        => ExecuteAsync(
            """
            INSERT INTO identity.authorization_policies (policy_id, permission_code, role_code, mode, limit_amount, max_count, window_seconds)
            VALUES (@id, @permission_code, @role_code, @mode, @limit_amount, @max_count, @window_seconds);
            """,
            ("id", Guid.NewGuid()),
            ("permission_code", permissionCode),
            ("role_code", roleCode),
            ("mode", mode),
            ("limit_amount", (object?)limitAmount ?? DBNull.Value),
            ("max_count", (object?)maxCount ?? DBNull.Value),
            ("window_seconds", (object?)windowSeconds ?? DBNull.Value));

    public Task SeedActiveDelegationAsync(Guid granteeUserId, Guid delegatorUserId, string permissionCode, decimal limitAmount)
        => ExecuteAsync(
            """
            INSERT INTO identity.authorization_delegations (delegation_id, permission_code, grantee_user_id, delegator_user_id, limit_amount, granted_at, expires_at)
            VALUES (@id, @permission_code, @grantee, @delegator, @limit_amount, now(), now() + interval '1 hour');
            """,
            ("id", Guid.NewGuid()),
            ("permission_code", permissionCode),
            ("grantee", granteeUserId),
            ("delegator", delegatorUserId),
            ("limit_amount", limitAmount));

    public Task SeedOpenBehaviouralTighteningAsync(Guid userId, string permissionCode)
        => ExecuteAsync(
            """
            INSERT INTO identity.behavioural_tightenings
                (tightening_id, user_id, permission_code, recent_count, baseline_per_window, trigger_ratio, triggered_at)
            VALUES (@id, @user_id, @permission_code, 5, 1.0, 5.0, now());
            """,
            ("id", Guid.NewGuid()),
            ("user_id", userId),
            ("permission_code", permissionCode));

    /// <summary>Simulates a manager approving a pending grant out of band (bypassing the HTTP decision surface, which this fixture doesn't wire).</summary>
    public Task ApproveGrantAsync(Guid grantId, Guid approverUserId)
        => ExecuteAsync(
            """
            UPDATE identity.authorization_grants
            SET status = 'granted', policy_path = 'manual', approver_user_id = @approver, resolved_at = now()
            WHERE grant_id = @grant_id;
            """,
            ("grant_id", grantId),
            ("approver", approverUserId));
}
