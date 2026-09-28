using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.CustomerData.AnonymizationState;

/// <summary>
/// Postgres-backed <see cref="ICustomerAnonymizationRequestStore"/> against
/// <c>customer_data.anonymization_requests</c> (migration 160, V14-CST-002).
/// </summary>
public sealed class PostgresCustomerAnonymizationRequestStore : ICustomerAnonymizationRequestStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresCustomerAnonymizationRequestStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Guid> CreateAsync(
        Guid customerId,
        AnonymizationRequestStatus initialStatus,
        string? requestedBy,
        string? blockedReason,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO customer_data.anonymization_requests
                (id, customer_id, status, requested_at, requested_by, blocked_reason, anonymized_at, row_version)
            VALUES (@id, @customer_id, @status, @requested_at, @requested_by, @blocked_reason, NULL, 1);
            """);
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = id;
        command.Parameters.Add("customer_id", NpgsqlDbType.Uuid).Value = customerId;
        command.Parameters.Add("status", NpgsqlDbType.Text).Value = initialStatus.ToString();
        command.Parameters.Add("requested_at", NpgsqlDbType.TimestampTz).Value = requestedAt;
        command.Parameters.Add("requested_by", NpgsqlDbType.Text).Value = (object?)requestedBy ?? DBNull.Value;
        command.Parameters.Add("blocked_reason", NpgsqlDbType.Text).Value = (object?)blockedReason ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);

        return id;
    }

    public async Task<CustomerAnonymizationRequest?> GetAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT id, customer_id, status, requested_at, requested_by, blocked_reason, anonymized_at, row_version
            FROM customer_data.anonymization_requests
            WHERE id = @id;
            """);
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = requestId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadRecord(reader);
    }

    public async Task UpdateStatusAsync(
        Guid requestId,
        AnonymizationRequestStatus newStatus,
        string? blockedReason,
        DateTimeOffset? anonymizedAt,
        int expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        await using var command = _dataSource.CreateCommand(
            """
            UPDATE customer_data.anonymization_requests
            SET status = @status,
                blocked_reason = @blocked_reason,
                anonymized_at = @anonymized_at,
                row_version = row_version + 1
            WHERE id = @id AND row_version = @expected_row_version;
            """);
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = requestId;
        command.Parameters.Add("status", NpgsqlDbType.Text).Value = newStatus.ToString();
        command.Parameters.Add("blocked_reason", NpgsqlDbType.Text).Value = (object?)blockedReason ?? DBNull.Value;
        command.Parameters.Add("anonymized_at", NpgsqlDbType.TimestampTz).Value = (object?)anonymizedAt ?? DBNull.Value;
        command.Parameters.Add("expected_row_version", NpgsqlDbType.Integer).Value = expectedRowVersion;

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (rows > 0)
            return;

        var existing = await GetAsync(requestId, cancellationToken);
        if (existing is null)
            throw new CustomerAnonymizationRequestNotFoundException(requestId);
        throw new CustomerAnonymizationConcurrencyException(requestId);
    }

    private static CustomerAnonymizationRequest ReadRecord(NpgsqlDataReader reader)
    {
        var id = reader.GetFieldValue<Guid>(0);
        var customerId = reader.GetFieldValue<Guid>(1);

        var rawStatus = reader.GetString(2);
        if (!Enum.TryParse<AnonymizationRequestStatus>(rawStatus, out var status))
            throw new FormatException($"Anonymization request {id} has an unrecognized status '{rawStatus}'.");

        var requestedAt = reader.GetFieldValue<DateTimeOffset>(3);
        var requestedBy = reader.IsDBNull(4) ? null : reader.GetString(4);
        var blockedReason = reader.IsDBNull(5) ? null : reader.GetString(5);
        var anonymizedAt = reader.IsDBNull(6) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(6);
        var rowVersion = reader.GetFieldValue<int>(7);

        return new CustomerAnonymizationRequest(id, customerId, status, requestedAt, requestedBy, blockedReason, anonymizedAt, rowVersion);
    }
}
