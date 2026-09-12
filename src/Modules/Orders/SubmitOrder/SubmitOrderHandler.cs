namespace ALKAROS.Orders.SubmitOrder;

using ALKAROS.Orders.OrderAggregate;
using Npgsql;

/// <summary>
/// Handles idempotent order submission (V1-ORD-002, PDF:II.2.4, PDF:II.3.2, PDF:III.6).
/// Guarantees that duplicate requests with the same (ClientId, OperationId) and identical
/// payload return the exact replayed result without duplicate order mutations, whereas
/// modified payloads with a reused key are rejected with conflict, and stale order versions
/// fail closed before any modification.
/// </summary>
public sealed class SubmitOrderHandler
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderSubmissionDispatcher? _dispatcher;
    private readonly TimeSpan _idempotencyRetention;

    public SubmitOrderHandler(
        NpgsqlDataSource dataSource,
        IOrderRepository orderRepository,
        TimeSpan? idempotencyRetention = null,
        IOrderSubmissionDispatcher? dispatcher = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _dispatcher = dispatcher;
        _idempotencyRetention = idempotencyRetention ?? TimeSpan.FromHours(24);
        if (_idempotencyRetention <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(idempotencyRetention), "Retention must be positive.");
    }

    public async Task<SubmitOrderResult> HandleAsync(
        SubmitOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        // Found by the WaiterPwa E2E load test (2026-09-12): several tables
        // ordering the SAME popular product at once can make two concurrent
        // submissions deadlock against each other purely from Postgres's
        // own INSERT ... ON CONFLICT DO UPDATE locking under heavy
        // contention on one stock_balances row (a documented Postgres
        // corner case, not a lock-ordering bug here - every attempt touches
        // its own order's stock items in the same stable order already).
        // The losing side's whole transaction rolls back with nothing
        // persisted, so retrying the entire attempt on a fresh connection
        // is safe - never a partial redo - and near-certain to succeed
        // once Postgres has broken the cycle by aborting one side.
        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await HandleAttemptAsync(command, cancellationToken).ConfigureAwait(false);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.DeadlockDetected && attempt < maxAttempts)
            {
                // A small random backoff before retrying, not an immediate
                // retry: several losers of the same deadlock cycle would
                // otherwise restart in lockstep and have a real chance of
                // immediately re-forming the same cycle against each other.
                var backoff = TimeSpan.FromMilliseconds(Random.Shared.Next(20, 120) * attempt);
                await Task.Delay(backoff, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<SubmitOrderResult> HandleAttemptAsync(
        SubmitOrderCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Validate();

        var requestHash = SubmitOrderRequestHash.Compute(command);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

        string? storedHash = null;
        byte[]? storedEnvelope = null;
        bool expired = false;
        bool found = false;

        // 1. Check existing idempotency key within transaction
        await using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.Transaction = transaction;
            checkCommand.CommandText =
                """
                SELECT request_hash, response_envelope, expires_at <= now() AS expired
                FROM idempotency_keys
                WHERE client_id = @client_id AND operation_id = @operation_id
                FOR UPDATE;
                """;
            checkCommand.Parameters.AddWithValue("client_id", command.ClientId);
            checkCommand.Parameters.AddWithValue("operation_id", command.OperationId);

            await using var reader = await checkCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                storedHash = reader.GetString(0).TrimEnd();
                storedEnvelope = reader.GetFieldValue<byte[]>(1);
                expired = reader.GetBoolean(2);
                found = true;
            }
        }

        if (found && !expired)
        {
            if (!string.Equals(storedHash, requestHash, StringComparison.Ordinal))
            {
                throw new SubmitOrderIdempotencyConflictException(command.ClientId, command.OperationId);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return SubmitOrderResponseSerializer.Deserialize(storedEnvelope!, isReplay: true);
        }

        // 2. First-time execution (or expired key replacement)
        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken).ConfigureAwait(false)
            ?? throw new OrderNotFoundException(command.OrderId);

        if (order.RowVersion != command.ExpectedRowVersion)
        {
            // Under concurrent execution, an earlier worker for this same idempotency key may have completed.
            string? recheckHash = null;
            byte[]? recheckEnvelope = null;
            bool recheckExpired = false;
            bool recheckFound = false;

            await using (var recheckCommand = connection.CreateCommand())
            {
                recheckCommand.Transaction = transaction;
                recheckCommand.CommandText =
                    """
                    SELECT request_hash, response_envelope, expires_at <= now() AS expired
                    FROM idempotency_keys
                    WHERE client_id = @client_id AND operation_id = @operation_id;
                    """;
                recheckCommand.Parameters.AddWithValue("client_id", command.ClientId);
                recheckCommand.Parameters.AddWithValue("operation_id", command.OperationId);

                await using var reader = await recheckCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    recheckHash = reader.GetString(0).TrimEnd();
                    recheckEnvelope = reader.GetFieldValue<byte[]>(1);
                    recheckExpired = reader.GetBoolean(2);
                    recheckFound = true;
                }
            }

            if (recheckFound && !recheckExpired && string.Equals(recheckHash, requestHash, StringComparison.Ordinal))
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return SubmitOrderResponseSerializer.Deserialize(recheckEnvelope!, isReplay: true);
            }

            throw new StaleOrderVersionException(order.Id, command.ExpectedRowVersion, order.RowVersion);
        }

        // V1-ORD-006: FireRound activates only the Draft items and hands back
        // exactly that round, so a check on its second round dispatches the
        // new lines alone. Submit() is now a thin wrapper over it and keeps
        // the Draft-only rule for callers that still mean "open this order".
        var (submitted, firedItems) = order.FireRound(
            command.Reason,
            command.ChangedBy,
            command.SubmittedAt);

        try
        {
            var newVersion = await _orderRepository.SaveAsync(submitted, command.ExpectedRowVersion, connection, transaction, cancellationToken).ConfigureAwait(false);

            if (_dispatcher is not null)
            {
                await _dispatcher.DispatchAsync(submitted, firedItems, connection, transaction, cancellationToken).ConfigureAwait(false);
            }

            var result = new SubmitOrderResult(
                submitted.Id,
                submitted.OrderNumber,
                submitted.Status,
                newVersion,
                submitted.SubmittedAt ?? DateTimeOffset.UtcNow,
                submitted.Total,
                submitted.Items.Count(i => i.IsActive),
                IsReplay: false);

            var responseEnvelope = SubmitOrderResponseSerializer.Serialize(result);

            // 3. Persist idempotency key atomically in the SAME transaction
            await using (var saveIdempotencyCommand = connection.CreateCommand())
            {
                saveIdempotencyCommand.Transaction = transaction;
                saveIdempotencyCommand.CommandText =
                    """
                    INSERT INTO idempotency_keys (client_id, operation_id, request_hash, response_envelope, expires_at)
                    VALUES (@client_id, @operation_id, @request_hash, @response_envelope, now() + @retention_seconds * interval '1 second')
                    ON CONFLICT (client_id, operation_id)
                    DO UPDATE SET request_hash = EXCLUDED.request_hash,
                                  response_envelope = EXCLUDED.response_envelope,
                                  expires_at = EXCLUDED.expires_at
                    WHERE idempotency_keys.request_hash = EXCLUDED.request_hash
                       OR idempotency_keys.expires_at <= now();
                    """;
                saveIdempotencyCommand.Parameters.AddWithValue("client_id", command.ClientId);
                saveIdempotencyCommand.Parameters.AddWithValue("operation_id", command.OperationId);
                saveIdempotencyCommand.Parameters.AddWithValue("request_hash", requestHash);
                saveIdempotencyCommand.Parameters.AddWithValue("response_envelope", responseEnvelope);
                saveIdempotencyCommand.Parameters.AddWithValue("retention_seconds", _idempotencyRetention.TotalSeconds);

                var affected = await saveIdempotencyCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                if (affected == 0)
                {
                    throw new SubmitOrderIdempotencyConflictException(command.ClientId, command.OperationId);
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (InvalidOperationException)
        {
            // Check if concurrent thread with same (client_id, operation_id) registered the result first
            await using var recheckCommand = _dataSource.CreateCommand(
                """
                SELECT request_hash, response_envelope, expires_at <= now() AS expired
                FROM idempotency_keys
                WHERE client_id = @client_id AND operation_id = @operation_id;
                """);
            recheckCommand.Parameters.AddWithValue("client_id", command.ClientId);
            recheckCommand.Parameters.AddWithValue("operation_id", command.OperationId);

            await using var reader = await recheckCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var catchStoredHash = reader.GetString(0).TrimEnd();
                var catchStoredEnvelope = reader.GetFieldValue<byte[]>(1);
                var catchExpired = reader.GetBoolean(2);

                if (!catchExpired && string.Equals(catchStoredHash, requestHash, StringComparison.Ordinal))
                {
                    return SubmitOrderResponseSerializer.Deserialize(catchStoredEnvelope, isReplay: true);
                }
            }

            throw;
        }
    }
}
