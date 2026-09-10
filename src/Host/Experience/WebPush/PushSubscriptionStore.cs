using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.WebPush;

/// <summary>V1-WTR-011: one device's push subscription as stored.</summary>
public sealed record PushSubscriptionRecord(
    Guid SubscriptionId,
    string Endpoint,
    string P256dh,
    string Auth,
    Guid UserId,
    Guid TerminalId);

/// <summary>
/// V1-WTR-011: the VAPID identity of this deployment. Created once and then
/// only read - regenerating it silently invalidates every subscription a
/// device already holds, so nothing here ever rewrites an existing pair.
/// </summary>
public sealed record VapidKeyPair(string PublicKey, string PrivateKey, string Subject);

public sealed class PushSubscriptionStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PushSubscriptionStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    /// <summary>
    /// Stores a subscription, replacing whatever was registered for the same
    /// endpoint. A browser reissues a subscription (same endpoint, new keys)
    /// whenever its own keys rotate, and the same physical device can also
    /// change hands between staff - both cases must update the row rather
    /// than add a second one.
    /// </summary>
    public async Task SaveAsync(PushSubscriptionRecord subscription, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        await using var cmd = _dataSource.CreateCommand(
            """
            INSERT INTO notifications.push_subscriptions
                (subscription_id, endpoint, p256dh, auth, user_id, terminal_id)
            VALUES (@subscription_id, @endpoint, @p256dh, @auth, @user_id, @terminal_id)
            ON CONFLICT (endpoint) DO UPDATE SET
                p256dh = EXCLUDED.p256dh,
                auth = EXCLUDED.auth,
                user_id = EXCLUDED.user_id,
                terminal_id = EXCLUDED.terminal_id,
                failure_count = 0;
            """);
        cmd.Parameters.Add("subscription_id", NpgsqlDbType.Uuid).Value = subscription.SubscriptionId;
        cmd.Parameters.Add("endpoint", NpgsqlDbType.Text).Value = subscription.Endpoint;
        cmd.Parameters.Add("p256dh", NpgsqlDbType.Text).Value = subscription.P256dh;
        cmd.Parameters.Add("auth", NpgsqlDbType.Text).Value = subscription.Auth;
        cmd.Parameters.Add("user_id", NpgsqlDbType.Uuid).Value = subscription.UserId;
        cmd.Parameters.Add("terminal_id", NpgsqlDbType.Uuid).Value = subscription.TerminalId;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PushSubscriptionRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var cmd = _dataSource.CreateCommand(
            """
            SELECT subscription_id, endpoint, p256dh, auth, user_id, terminal_id
            FROM notifications.push_subscriptions;
            """);
        var results = new List<PushSubscriptionRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PushSubscriptionRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetGuid(4),
                reader.GetGuid(5)));
        }
        return results;
    }

    /// <summary>
    /// RFC 8030 §7.3: a push service answers 404 or 410 once a subscription is
    /// permanently gone. Keeping the row would mean re-sending to a dead
    /// endpoint on every notification for the life of the deployment.
    /// </summary>
    public async Task DeleteByEndpointAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        await using var cmd = _dataSource.CreateCommand(
            "DELETE FROM notifications.push_subscriptions WHERE endpoint = @endpoint;");
        cmd.Parameters.Add("endpoint", NpgsqlDbType.Text).Value = endpoint;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// V1-RMD-160: found by the 2026-09-10 Garson audit — the HTTP delete
    /// endpoint used to call <see cref="DeleteByEndpointAsync"/> directly,
    /// which deletes whatever row matches that endpoint string with no
    /// owner check at all. Any authenticated cashier session (any terminal,
    /// any staff member) that knew or guessed another device's endpoint URL
    /// could unsubscribe it. This overload is for that caller only —
    /// <see cref="DeleteByEndpointAsync"/> stays as-is for the two internal,
    /// no-user-context callers (<c>WebPushSender</c>'s own permanent-failure
    /// cleanup, RFC 8030 §7.3), which have no principal to scope by and are
    /// cleaning up a dead subscription regardless of who it belonged to.
    /// </summary>
    public async Task DeleteByEndpointForUserAsync(string endpoint, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var cmd = _dataSource.CreateCommand(
            "DELETE FROM notifications.push_subscriptions WHERE endpoint = @endpoint AND user_id = @user_id;");
        cmd.Parameters.Add("endpoint", NpgsqlDbType.Text).Value = endpoint;
        cmd.Parameters.Add("user_id", NpgsqlDbType.Uuid).Value = userId;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkSuccessAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        await using var cmd = _dataSource.CreateCommand(
            """
            UPDATE notifications.push_subscriptions
            SET last_success_at = now(), failure_count = 0
            WHERE endpoint = @endpoint;
            """);
        cmd.Parameters.Add("endpoint", NpgsqlDbType.Text).Value = endpoint;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkFailureAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        await using var cmd = _dataSource.CreateCommand(
            """
            UPDATE notifications.push_subscriptions
            SET failure_count = failure_count + 1
            WHERE endpoint = @endpoint;
            """);
        cmd.Parameters.Add("endpoint", NpgsqlDbType.Text).Value = endpoint;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Reads the deployment's VAPID pair, creating it on first use. The insert
    /// is conditional on the row being absent, so two hosts starting at once
    /// cannot end up with two identities - the loser reads the winner's row.
    /// </summary>
    public async Task<VapidKeyPair> GetOrCreateVapidKeysAsync(string subject, CancellationToken cancellationToken = default)
    {
        var existing = await ReadVapidKeysAsync(cancellationToken);
        if (existing is not null) return existing;

        var (publicKey, privateKey) = WebPushCrypto.CreateKeyPair();
        await using (var insert = _dataSource.CreateCommand(
            """
            INSERT INTO notifications.vapid_keys (single_row, public_key, private_key, subject)
            VALUES (TRUE, @public_key, @private_key, @subject)
            ON CONFLICT (single_row) DO NOTHING;
            """))
        {
            insert.Parameters.Add("public_key", NpgsqlDbType.Text).Value = publicKey;
            insert.Parameters.Add("private_key", NpgsqlDbType.Text).Value = privateKey;
            insert.Parameters.Add("subject", NpgsqlDbType.Text).Value = subject;
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        return await ReadVapidKeysAsync(cancellationToken)
               ?? throw new InvalidOperationException("The VAPID key pair could not be read back after creation.");
    }

    private async Task<VapidKeyPair?> ReadVapidKeysAsync(CancellationToken cancellationToken)
    {
        await using var cmd = _dataSource.CreateCommand(
            "SELECT public_key, private_key, subject FROM notifications.vapid_keys WHERE single_row;");
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new VapidKeyPair(reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }
}
