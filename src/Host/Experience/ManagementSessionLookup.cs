using ALKAROS.Identity.DeviceSessions;
using Npgsql;

namespace ALKAROS.Host.Experience;

/// <summary>
/// Shared manager/supervisor device-session lookup behind the Roles,
/// Authorization-decision and Catalog-management Experience areas' own
/// endpoint authentication filters. V1-RMD-121: found duplicated
/// byte-for-byte (except the supervisor-inclusion flag) as
/// RoleManagementAuthentication, AuthorizationDecisionAuthentication and
/// CatalogManagerAuthentication by an independent boundary audit
/// (2026-09-07) — a code-duplication/maintenance risk (a future change to
/// one copy could silently not apply to the others), not a module-boundary
/// violation: each caller reads identity's own schema for its own session
/// check, which V0-ARC-001 always allows.
/// </summary>
internal static class ManagementSessionLookup
{
    private const string ManagerOnlySql = """
        UPDATE identity.device_sessions AS session
        SET last_seen_at = now()
        FROM identity.users AS actor
        WHERE session.user_id = actor.user_id
          AND actor.active
          AND session.token_hash = @token_hash
          AND session.device_id LIKE 'manager:%'
          AND session.revoked_at IS NULL
          AND session.expires_at > now()
        RETURNING session.user_id;
        """;

    private const string ManagerOrSupervisorSql = """
        UPDATE identity.device_sessions AS session
        SET last_seen_at = now()
        FROM identity.users AS actor
        WHERE session.user_id = actor.user_id
          AND actor.active
          AND session.token_hash = @token_hash
          AND (session.device_id LIKE 'manager:%' OR session.device_id LIKE 'supervisor:%')
          AND session.revoked_at IS NULL
          AND session.expires_at > now()
        RETURNING session.user_id;
        """;

    /// <summary>
    /// Resolves the acting user id from a raw cookie token, refreshing
    /// <c>last_seen_at</c> as a side effect. Returns null when the token is
    /// missing, unknown, revoked, expired, or bound to a device role outside
    /// <paramref name="allowSupervisor"/>'s scope.
    /// </summary>
    public static async Task<Guid?> ResolveActorAsync(
        NpgsqlDataSource dataSource,
        string? rawToken,
        bool allowSupervisor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return null;

        await using var command = dataSource.CreateCommand(allowSupervisor ? ManagerOrSupervisorSql : ManagerOnlySql);
        command.Parameters.AddWithValue("token_hash", DeviceSessionToken.Hash(rawToken));
        var actorId = await command.ExecuteScalarAsync(cancellationToken);
        return actorId is Guid value ? value : null;
    }
}
