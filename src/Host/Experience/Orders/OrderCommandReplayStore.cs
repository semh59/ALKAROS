using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.Identity.Authorization.Grants;
using Npgsql;

namespace ALKAROS.Host.Experience.Orders;

/// <summary>
/// Makes a repeated void or complimentary command answer with its first result: the client resends the same key when the
/// answer was lost, and the second request would otherwise meet the new row version and report a conflict although the
/// work was done. The key is scoped to the terminal, the user and the command; the same key with a different request is
/// refused; two requests with one key run one after the other.
/// </summary>
public sealed class OrderCommandReplayStore
{
    private const int MaxKeyLength = 100;
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly NpgsqlDataSource _dataSource;

    public OrderCommandReplayStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<T> ExecuteAsync<T>(
        string scope, string? key, object request, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (string.IsNullOrWhiteSpace(key))
            return await action().ConfigureAwait(false);
        if (key.Length > MaxKeyLength || key.Any(char.IsControl))
            throw new ArgumentException("The idempotency key is not valid.", nameof(key));

        var requestHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{scope}\n{JsonSerializer.Serialize(request, request.GetType(), Json)}"))).ToLowerInvariant();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await LockAsync(connection, "SELECT pg_advisory_lock(hashtextextended(@scope, 0));", scope, key, cancellationToken).ConfigureAwait(false);
        try
        {
            var stored = await FindAsync(connection, scope, key, cancellationToken).ConfigureAwait(false);
            if (stored is { } found)
            {
                if (found.RequestHash != requestHash)
                    throw new IdempotencyKeyReusedException(key);
                return JsonSerializer.Deserialize<T>(found.Body, Json)!;
            }

            var result = await action().ConfigureAwait(false);
            await StoreAsync(connection, scope, key, requestHash, JsonSerializer.SerializeToUtf8Bytes(result, Json), cancellationToken)
                .ConfigureAwait(false);
            return result;
        }
        finally
        {
            await LockAsync(connection, "SELECT pg_advisory_unlock(hashtextextended(@scope, 0));", scope, key, CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private static async Task LockAsync(NpgsqlConnection connection, string sql, string scope, string key, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("scope", $"{scope}|{key}");
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<(string RequestHash, byte[] Body)?> FindAsync(
        NpgsqlConnection connection, string scope, string key, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT request_hash, response_envelope FROM idempotency_keys WHERE client_id = @client_id AND operation_id = @operation_id AND expires_at > now();",
            connection);
        command.Parameters.AddWithValue("client_id", scope);
        command.Parameters.AddWithValue("operation_id", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? (reader.GetString(0).TrimEnd(), reader.GetFieldValue<byte[]>(1))
            : null;
    }

    private static async Task StoreAsync(
        NpgsqlConnection connection, string scope, string key, string requestHash, byte[] body, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO idempotency_keys (client_id, operation_id, request_hash, response_envelope, expires_at)
            VALUES (@client_id, @operation_id, @request_hash, @body, now() + @retention)
            ON CONFLICT (client_id, operation_id) DO UPDATE
            SET request_hash = EXCLUDED.request_hash, response_envelope = EXCLUDED.response_envelope,
                created_at = now(), expires_at = EXCLUDED.expires_at;
            """,
            connection);
        command.Parameters.AddWithValue("client_id", scope);
        command.Parameters.AddWithValue("operation_id", key);
        command.Parameters.AddWithValue("request_hash", requestHash);
        command.Parameters.AddWithValue("body", body);
        command.Parameters.AddWithValue("retention", Retention);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
