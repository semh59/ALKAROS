using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ALKAROS.Host.Experience.Tables;

/// <summary>
/// Makes a repeated table command (merge, unmerge, transfer) answer with its first result instead of a 404 or 409.
/// The key is scoped to the terminal and the signed-in user, the same key with a different request is refused, and two
/// requests with one key run one after the other. A request without the header runs as before.
/// </summary>
internal sealed class TableIdempotencyFilter : IEndpointFilter
{
    public const string HeaderName = "X-Idempotency-Key";
    private const int MaxKeyLength = 100;
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    private readonly NpgsqlDataSource _dataSource;
    private readonly ITableManagementSessionAuthorizer _authorizer;
    private readonly JsonSerializerOptions _json;

    public TableIdempotencyFilter(
        NpgsqlDataSource dataSource,
        ITableManagementSessionAuthorizer authorizer,
        IOptions<JsonOptions> json)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
        _json = json?.Value.SerializerOptions ?? throw new ArgumentNullException(nameof(json));
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var key = http.Request.Headers[HeaderName].ToString();
        if (key.Length == 0)
            return await next(context);
        if (key.Length > MaxKeyLength || key.Any(char.IsControl))
            throw new ArgumentException($"{HeaderName} is not valid.");

        var cancellationToken = http.RequestAborted;
        var terminalId = Guid.Parse(http.Request.RouteValues["terminalId"]!.ToString()!);
        var principal = await _authorizer.RequireReadAsync(http, terminalId, cancellationToken);
        var clientId = $"tables:{terminalId:N}:{principal.UserId:N}";
        var requestHash = Hash(http, context);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await LockAsync(connection, "SELECT pg_advisory_lock(hashtextextended(@scope, 0));", clientId, key, cancellationToken);
        try
        {
            var stored = await FindAsync(connection, clientId, key, cancellationToken);
            if (stored is { } found)
            {
                if (found.RequestHash != requestHash)
                    throw new TableOperationBlockedException("IDEMPOTENCY_KEY_REUSED", "Bu anahtar başka bir istekle kullanılmış.");
                return Results.Content(Encoding.UTF8.GetString(found.Body), "application/json", Encoding.UTF8, StatusCodes.Status200OK);
            }

            var result = await next(context);
            if (result is IValueHttpResult { Value: { } value } and IStatusCodeHttpResult { StatusCode: StatusCodes.Status200OK })
                await StoreAsync(connection, clientId, key, requestHash, JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), _json), cancellationToken);
            return result;
        }
        finally
        {
            await LockAsync(connection, "SELECT pg_advisory_unlock(hashtextextended(@scope, 0));", clientId, key, CancellationToken.None);
        }
    }

    private static string Hash(HttpContext http, EndpointFilterInvocationContext context)
    {
        var request = context.Arguments.FirstOrDefault(argument =>
            argument is not null && argument.GetType().Namespace == typeof(TableIdempotencyFilter).Namespace
            && argument.GetType().Name.EndsWith("Request", StringComparison.Ordinal));
        var text = $"{http.Request.Method} {http.Request.Path}\n{JsonSerializer.Serialize(request, request?.GetType() ?? typeof(object))}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    private static async Task LockAsync(NpgsqlConnection connection, string sql, string clientId, string key, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("scope", $"{clientId}|{key}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<(string RequestHash, byte[] Body)?> FindAsync(
        NpgsqlConnection connection, string clientId, string key, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT request_hash, response_envelope FROM idempotency_keys WHERE client_id = @client_id AND operation_id = @operation_id AND expires_at > now();",
            connection);
        command.Parameters.AddWithValue("client_id", clientId);
        command.Parameters.AddWithValue("operation_id", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? (reader.GetString(0).TrimEnd(), reader.GetFieldValue<byte[]>(1)) : null;
    }

    private static async Task StoreAsync(
        NpgsqlConnection connection, string clientId, string key, string requestHash, byte[] body, CancellationToken cancellationToken)
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
        command.Parameters.AddWithValue("client_id", clientId);
        command.Parameters.AddWithValue("operation_id", key);
        command.Parameters.AddWithValue("request_hash", requestHash);
        command.Parameters.AddWithValue("body", body);
        command.Parameters.AddWithValue("retention", Retention);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
