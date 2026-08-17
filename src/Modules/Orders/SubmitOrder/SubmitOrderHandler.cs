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
    private readonly TimeSpan _idempotencyRetention;

    public SubmitOrderHandler(
        NpgsqlDataSource dataSource,
        IOrderRepository orderRepository,
        TimeSpan? idempotencyRetention = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _idempotencyRetention = idempotencyRetention ?? TimeSpan.FromHours(24);
        if (_idempotencyRetention <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(idempotencyRetention), "Retention must be positive.");
    }

    public async Task<SubmitOrderResult> HandleAsync(
        SubmitOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Validate();

        var requestHash = SubmitOrderRequestHash.Compute(command);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        // 1. Check existing idempotency key
        await using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText =
                """
                SELECT request_hash, response_envelope, expires_at <= now() AS expired
                FROM idempotency_keys
                WHERE client_id = @client_id AND operation_id = @operation_id;
                """;
            checkCommand.Parameters.AddWithValue("client_id", command.ClientId);
            checkCommand.Parameters.AddWithValue("operation_id", command.OperationId);

            await using var reader = await checkCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var storedHash = reader.GetString(0).TrimEnd();
                var storedEnvelope = reader.GetFieldValue<byte[]>(1);
                var expired = reader.GetBoolean(2);

                if (!expired)
                {
                    if (!string.Equals(storedHash, requestHash, StringComparison.Ordinal))
                    {
                        throw new SubmitOrderIdempotencyConflictException(command.ClientId, command.OperationId);
                    }

                    return SubmitOrderResponseSerializer.Deserialize(storedEnvelope, isReplay: true);
                }
            }
        }

        // 2. First-time execution (or expired key replacement)
        var order = await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken).ConfigureAwait(false)
            ?? throw new OrderNotFoundException(command.OrderId);

        if (order.RowVersion != command.ExpectedRowVersion)
        {
            // Under concurrent execution, an earlier worker for this same idempotency key may have completed.
            await using var recheckCommand = connection.CreateCommand();
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
                var storedHash = reader.GetString(0).TrimEnd();
                var storedEnvelope = reader.GetFieldValue<byte[]>(1);
                var expired = reader.GetBoolean(2);

                if (!expired && string.Equals(storedHash, requestHash, StringComparison.Ordinal))
                {
                    return SubmitOrderResponseSerializer.Deserialize(storedEnvelope, isReplay: true);
                }
            }

            throw new StaleOrderVersionException(order.Id, command.ExpectedRowVersion, order.RowVersion);
        }

        if (!order.CanTransitionTo(OrderState.Submitted))
        {
            throw new InvalidOperationException(
                $"Order {order.Id} cannot transition from {order.Status} to {OrderState.Submitted}.");
        }

        var submitted = order.TransitionTo(
            OrderState.Submitted,
            command.Reason,
            command.ChangedBy,
            command.SubmittedAt);

        try
        {
            var newVersion = await _orderRepository.SaveAsync(submitted, command.ExpectedRowVersion, cancellationToken).ConfigureAwait(false);

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

            // 3. Persist idempotency key atomically
            await using (var saveIdempotencyCommand = connection.CreateCommand())
            {
                saveIdempotencyCommand.CommandText =
                    """
                    INSERT INTO idempotency_keys (client_id, operation_id, request_hash, response_envelope, expires_at)
                    VALUES (@client_id, @operation_id, @request_hash, @response_envelope, now() + @retention_seconds * interval '1 second')
                    ON CONFLICT (client_id, operation_id)
                    DO UPDATE SET request_hash = EXCLUDED.request_hash,
                                  response_envelope = EXCLUDED.response_envelope,
                                  expires_at = EXCLUDED.expires_at;
                    """;
                saveIdempotencyCommand.Parameters.AddWithValue("client_id", command.ClientId);
                saveIdempotencyCommand.Parameters.AddWithValue("operation_id", command.OperationId);
                saveIdempotencyCommand.Parameters.AddWithValue("request_hash", requestHash);
                saveIdempotencyCommand.Parameters.AddWithValue("response_envelope", responseEnvelope);
                saveIdempotencyCommand.Parameters.AddWithValue("retention_seconds", _idempotencyRetention.TotalSeconds);

                await saveIdempotencyCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        catch (InvalidOperationException)
        {
            // Check if concurrent thread with same (client_id, operation_id) registered the result first
            await using var recheckCommand = connection.CreateCommand();
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
                var storedHash = reader.GetString(0).TrimEnd();
                var storedEnvelope = reader.GetFieldValue<byte[]>(1);
                var expired = reader.GetBoolean(2);

                if (!expired && string.Equals(storedHash, requestHash, StringComparison.Ordinal))
                {
                    return SubmitOrderResponseSerializer.Deserialize(storedEnvelope, isReplay: true);
                }
            }

            throw;
        }
    }
}
