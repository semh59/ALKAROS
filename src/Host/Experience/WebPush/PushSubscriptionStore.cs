using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
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
    /// <summary>One person's phones and tablets; the oldest registration is dropped beyond this.</summary>
    public const int MaximumSubscriptionsPerUser = 10;

    private const string VapidPrivateKeyField = "vapid_private_key";
    private static readonly SecretReference MasterKey = new("envelope-master-key");

    private readonly NpgsqlDataSource _dataSource;
    private readonly SensitivePayloadProtector _protector;

    public PushSubscriptionStore(NpgsqlDataSource dataSource, ISecretProvider secretProvider)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        ArgumentNullException.ThrowIfNull(secretProvider);

        var policy = new PushVapidAccessPolicy();
        _protector = new SensitivePayloadProtector(new AesGcmEnvelopeCipher(new SecretResolver(secretProvider, policy)), policy);
    }

    /// <summary>
    /// Stores a subscription, replacing whatever was registered for the same
    /// endpoint. A browser reissues a subscription (same endpoint, new keys)
    /// whenever its own keys rotate, and the same physical device can also
    /// change hands between staff - both cases must update the row rather
    /// than add a second one. An endpoint that belongs to another terminal
    /// and another user is not taken over: returns false and leaves the row
    /// alone. Beyond <see cref="MaximumSubscriptionsPerUser"/> the user's
    /// oldest registrations are removed.
    /// </summary>
    public async Task<bool> SaveAsync(PushSubscriptionRecord subscription, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var save = new NpgsqlCommand(
            """
            INSERT INTO notifications.push_subscriptions
                (subscription_id, endpoint, p256dh, auth, user_id, terminal_id)
            VALUES (@subscription_id, @endpoint, @p256dh, @auth, @user_id, @terminal_id)
            ON CONFLICT (endpoint) DO UPDATE SET
                p256dh = EXCLUDED.p256dh,
                auth = EXCLUDED.auth,
                user_id = EXCLUDED.user_id,
                terminal_id = EXCLUDED.terminal_id,
                created_at = now(),
                failure_count = 0
            WHERE notifications.push_subscriptions.terminal_id = EXCLUDED.terminal_id
               OR notifications.push_subscriptions.user_id = EXCLUDED.user_id;
            """, connection, transaction);
        save.Parameters.Add("subscription_id", NpgsqlDbType.Uuid).Value = subscription.SubscriptionId;
        save.Parameters.Add("endpoint", NpgsqlDbType.Text).Value = subscription.Endpoint;
        save.Parameters.Add("p256dh", NpgsqlDbType.Text).Value = subscription.P256dh;
        save.Parameters.Add("auth", NpgsqlDbType.Text).Value = subscription.Auth;
        save.Parameters.Add("user_id", NpgsqlDbType.Uuid).Value = subscription.UserId;
        save.Parameters.Add("terminal_id", NpgsqlDbType.Uuid).Value = subscription.TerminalId;
        if (await save.ExecuteNonQueryAsync(cancellationToken) == 0)
            return false;

        await using var prune = new NpgsqlCommand(
            """
            DELETE FROM notifications.push_subscriptions
            WHERE subscription_id IN (
                SELECT subscription_id
                FROM notifications.push_subscriptions
                WHERE user_id = @user_id
                ORDER BY created_at DESC, subscription_id
                OFFSET @maximum);
            """, connection, transaction);
        prune.Parameters.Add("user_id", NpgsqlDbType.Uuid).Value = subscription.UserId;
        prune.Parameters.Add("maximum", NpgsqlDbType.Integer).Value = MaximumSubscriptionsPerUser;
        await prune.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    /// <summary>Every device of an active user; a deactivated person's devices are never notified.</summary>
    public async Task<IReadOnlyList<PushSubscriptionRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var cmd = _dataSource.CreateCommand(
            """
            SELECT s.subscription_id, s.endpoint, s.p256dh, s.auth, s.user_id, s.terminal_id
            FROM notifications.push_subscriptions s
            JOIN identity.users u ON u.user_id = s.user_id AND u.active;
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
    /// V1-RMD-201: every device a specific user has ever subscribed from —
    /// used to target a single waiter once an order's serving user is known,
    /// instead of <see cref="GetAllAsync"/>'s deployment-wide broadcast set.
    /// </summary>
    public async Task<IReadOnlyList<PushSubscriptionRecord>> GetByUserAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        await using var cmd = _dataSource.CreateCommand(
            """
            SELECT s.subscription_id, s.endpoint, s.p256dh, s.auth, s.user_id, s.terminal_id
            FROM notifications.push_subscriptions s
            JOIN identity.users u ON u.user_id = s.user_id AND u.active
            WHERE s.user_id = @user_id;
            """);
        cmd.Parameters.Add("user_id", NpgsqlDbType.Uuid).Value = userId;
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

    /// <summary>
    /// Counts one more failed delivery and removes the subscription once
    /// <paramref name="maximumFailures"/> have failed in a row (a success
    /// resets the count), so a permanently broken endpoint is not retried
    /// on every notification for the life of the deployment.
    /// </summary>
    public async Task MarkFailureAsync(string endpoint, int maximumFailures, CancellationToken cancellationToken = default)
    {
        await using var cmd = _dataSource.CreateCommand(
            """
            UPDATE notifications.push_subscriptions
            SET failure_count = failure_count + 1
            WHERE endpoint = @endpoint;
            DELETE FROM notifications.push_subscriptions
            WHERE endpoint = @endpoint AND failure_count >= @maximum;
            """);
        cmd.Parameters.Add("endpoint", NpgsqlDbType.Text).Value = endpoint;
        cmd.Parameters.Add("maximum", NpgsqlDbType.Integer).Value = maximumFailures;
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
            INSERT INTO notifications.vapid_keys (single_row, public_key, private_key_envelope, subject)
            VALUES (TRUE, @public_key, @private_key_envelope, @subject)
            ON CONFLICT (single_row) DO NOTHING;
            """))
        {
            insert.Parameters.Add("public_key", NpgsqlDbType.Text).Value = publicKey;
            insert.Parameters.Add("private_key_envelope", NpgsqlDbType.Bytea).Value = Seal(privateKey);
            insert.Parameters.Add("subject", NpgsqlDbType.Text).Value = subject;
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        return await ReadVapidKeysAsync(cancellationToken)
               ?? throw new InvalidOperationException("The VAPID key pair could not be read back after creation.");
    }

    private async Task<VapidKeyPair?> ReadVapidKeysAsync(CancellationToken cancellationToken)
    {
        string publicKey;
        string? plaintextPrivateKey;
        byte[]? envelope;
        string subject;
        await using (var cmd = _dataSource.CreateCommand(
            "SELECT public_key, private_key, private_key_envelope, subject FROM notifications.vapid_keys WHERE single_row;"))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) return null;
            publicKey = reader.GetString(0);
            plaintextPrivateKey = reader.IsDBNull(1) ? null : reader.GetString(1);
            envelope = reader.IsDBNull(2) ? null : reader.GetFieldValue<byte[]>(2);
            subject = reader.GetString(3);
        }

        if (envelope is not null)
            return new VapidKeyPair(publicKey, Open(envelope), subject);

        // A key stored in plaintext by an earlier version: protect it now and clear the plaintext column.
        await using var protect = _dataSource.CreateCommand(
            """
            UPDATE notifications.vapid_keys
            SET private_key_envelope = @private_key_envelope, private_key = NULL
            WHERE single_row AND private_key_envelope IS NULL;
            """);
        protect.Parameters.Add("private_key_envelope", NpgsqlDbType.Bytea).Value = Seal(plaintextPrivateKey!);
        await protect.ExecuteNonQueryAsync(cancellationToken);
        return new VapidKeyPair(publicKey, plaintextPrivateKey!, subject);
    }

    private byte[] Seal(string privateKey)
    {
        var payload = new SensitivePayload(
            new Dictionary<string, string> { [VapidPrivateKeyField] = privateKey },
            new Dictionary<string, SensitiveCategory> { [VapidPrivateKeyField] = SensitiveCategory.Credential });
        return _protector.Protect(payload, MasterKey, PushVapidAccessPolicy.Accessor).ToPersistenceBytes();
    }

    private string Open(byte[] envelope)
        => _protector.Unprotect(SensitiveEnvelope.FromPersistenceBytes(envelope), MasterKey, PushVapidAccessPolicy.Accessor)
            .Fields[VapidPrivateKeyField];
}
