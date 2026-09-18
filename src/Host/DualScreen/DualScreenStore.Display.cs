using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.Identity.DeviceSessions;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.DualScreen;

public sealed partial class DualScreenStore
{
    public async Task<PairingRequestCreated> CreatePairingRequestAsync(Guid displayId, CancellationToken cancellationToken)
    {
        EnsureNotEmpty(displayId, nameof(displayId));
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var requestId = Guid.NewGuid();
            var (secret, secretHash) = DualScreenToken.Create("alkaros-display-pairing:");
            var code = CreatePairingCode();
            var expiresAt = DateTimeOffset.UtcNow.Add(PairingLifetime);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await using (var closeOld = CreateCommand(connection, transaction,
                    """
                    UPDATE customer_display.pairing_requests
                    SET consumed_at = now()
                    WHERE consumed_at IS NULL
                      AND (display_id = @display_id OR expires_at <= now());
                    """))
                {
                    closeOld.Parameters.AddWithValue("display_id", displayId);
                    await closeOld.ExecuteNonQueryAsync(cancellationToken);
                }

                await using (var insert = CreateCommand(connection, transaction,
                    """
                    INSERT INTO customer_display.pairing_requests (
                        request_id, display_id, terminal_id, pairing_secret_hash, code_hash,
                        expires_at, approved_at, consumed_at, failed_attempts, created_at)
                    VALUES (@request_id, @display_id, NULL, @secret_hash, @code_hash,
                            @expires_at, NULL, NULL, 0, now());
                    """))
                {
                    insert.Parameters.AddWithValue("request_id", requestId);
                    insert.Parameters.AddWithValue("display_id", displayId);
                    insert.Parameters.AddWithValue("secret_hash", secretHash);
                    insert.Parameters.AddWithValue("code_hash", DualScreenToken.Hash(NormalizeCode(code)));
                    insert.Parameters.AddWithValue("expires_at", expiresAt);
                    await insert.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                return new PairingRequestCreated(requestId, displayId, secret, code, expiresAt);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
        }

        throw new DualScreenConflictException("A unique pairing code could not be allocated.");
    }

    public async Task ApprovePairingAsync(Guid terminalId, string code, CancellationToken cancellationToken)
    {
        var normalized = NormalizeCode(code);
        await EnsureTerminalAsync(terminalId, cancellationToken);
        await using var command = _dataSource.CreateCommand(
            """
            UPDATE customer_display.pairing_requests
            SET terminal_id = @terminal_id, approved_at = now()
            WHERE request_id = (
                SELECT request_id
                FROM customer_display.pairing_requests
                WHERE code_hash = @code_hash
                  AND expires_at > now()
                  AND approved_at IS NULL
                  AND consumed_at IS NULL
                  AND failed_attempts < 5
                FOR UPDATE SKIP LOCKED
            )
            RETURNING request_id;
            """);
        command.Parameters.AddWithValue("terminal_id", terminalId);
        command.Parameters.AddWithValue("code_hash", DualScreenToken.Hash(normalized));
        if (await command.ExecuteScalarAsync(cancellationToken) is null)
            throw new DualScreenNotFoundException("Pairing code is invalid or expired.");
    }

    public async Task<(PairingCompleted Result, string RawToken)> CompletePairingAsync(
        Guid requestId,
        string secret,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        PairingRow row;
        await using (var select = CreateCommand(connection, transaction,
            """
            SELECT display_id, terminal_id, pairing_secret_hash, expires_at, approved_at, consumed_at, failed_attempts
            FROM customer_display.pairing_requests
            WHERE request_id = @request_id
            FOR UPDATE;
            """))
        {
            select.Parameters.AddWithValue("request_id", requestId);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new DualScreenNotFoundException("Pairing request was not found.");
            row = new PairingRow(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetGuid(1),
                reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3),
                reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
                reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
                reader.GetInt16(6));
        }

        if (!DualScreenToken.Matches(secret, row.SecretHash))
        {
            await using var fail = CreateCommand(connection, transaction,
                """
                UPDATE customer_display.pairing_requests
                SET failed_attempts = LEAST(failed_attempts + 1, 5)
                WHERE request_id = @request_id;
                """);
            fail.Parameters.AddWithValue("request_id", requestId);
            await fail.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new DualScreenUnauthorizedException("Pairing secret is invalid.");
        }

        if (row.ExpiresAt <= DateTimeOffset.UtcNow || row.ApprovedAt is null || row.ConsumedAt is not null || row.TerminalId is null)
            throw new DualScreenConflictException("Pairing request is not approved, has expired, or was consumed.");

        var (rawToken, tokenHash) = DualScreenToken.Create("alkaros-display-session:");
        var sessionId = Guid.NewGuid();
        var sessionExpiresAt = DateTimeOffset.UtcNow.Add(DisplaySessionLifetime);

        await using (var revoke = CreateCommand(connection, transaction,
            """
            UPDATE customer_display.display_sessions
            SET revoked_at = now()
            WHERE display_id = @display_id AND revoked_at IS NULL;
            """))
        {
            revoke.Parameters.AddWithValue("display_id", row.DisplayId);
            await revoke.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var create = CreateCommand(connection, transaction,
            """
            INSERT INTO customer_display.display_sessions (
                session_id, display_id, terminal_id, token_hash, created_at, expires_at, revoked_at, last_seen_at)
            VALUES (@session_id, @display_id, @terminal_id, @token_hash, now(), @expires_at, NULL, now());
            """))
        {
            create.Parameters.AddWithValue("session_id", sessionId);
            create.Parameters.AddWithValue("display_id", row.DisplayId);
            create.Parameters.AddWithValue("terminal_id", row.TerminalId.Value);
            create.Parameters.AddWithValue("token_hash", tokenHash);
            create.Parameters.AddWithValue("expires_at", sessionExpiresAt);
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var consume = CreateCommand(connection, transaction,
            "UPDATE customer_display.pairing_requests SET consumed_at = now() WHERE request_id = @request_id;"))
        {
            consume.Parameters.AddWithValue("request_id", requestId);
            await consume.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return (new PairingCompleted(row.DisplayId, row.TerminalId.Value, sessionExpiresAt), rawToken);
    }

    public async Task<int> RevokeDisplaySessionsAsync(Guid terminalId, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            UPDATE customer_display.display_sessions
            SET revoked_at = now()
            WHERE terminal_id = @terminal_id AND revoked_at IS NULL;
            """);
        command.Parameters.AddWithValue("terminal_id", terminalId);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<CustomerDisplaySnapshotDto> GetSnapshotAsync(
        Guid displayId,
        Guid terminalId,
        CancellationToken cancellationToken)
    {
        Guid? orderId;
        await using (var terminalCommand = _dataSource.CreateCommand(
            "SELECT active_order_id FROM customer_display.terminals WHERE terminal_id = @terminal_id;"))
        {
            terminalCommand.Parameters.AddWithValue("terminal_id", terminalId);
            var result = await terminalCommand.ExecuteScalarAsync(cancellationToken);
            orderId = result is null or DBNull ? null : (Guid)result;
        }

        if (orderId is null)
            return IdleSnapshot(displayId, terminalId);

        OrderRow order;
        await using (var orderCommand = _dataSource.CreateCommand(
            """
            SELECT order_number, status, subtotal, discount_total, tax_total, total, currency_code, row_version
            FROM orders.orders
            WHERE order_id = @order_id;
            """))
        {
            orderCommand.Parameters.AddWithValue("order_id", orderId.Value);
            await using var reader = await orderCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return IdleSnapshot(displayId, terminalId);
            order = new OrderRow(
                reader.GetString(0), reader.GetString(1), reader.GetDecimal(2), reader.GetDecimal(3),
                reader.GetDecimal(4), reader.GetDecimal(5), reader.GetString(6), reader.GetInt64(7));
        }

        var lines = new List<CustomerDisplayLineDto>();
        await using (var lineCommand = _dataSource.CreateCommand(
            """
            SELECT order_item_id, product_name_snapshot, quantity, unit_price, gross_amount, status, tax_rate
            FROM orders.order_items
            WHERE order_id = @order_id AND status IN ('Draft', 'Active', 'Complimentary')
            ORDER BY created_at, order_item_id;
            """))
        {
            lineCommand.Parameters.AddWithValue("order_id", orderId.Value);
            await using var reader = await lineCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var quantity = reader.GetDecimal(2);
                var unitPrice = reader.GetDecimal(3);
                var storedGrossAmount = reader.GetDecimal(4);
                var status = reader.GetString(5);
                // V1-CUI-011: a Complimentary item's own persisted
                // gross_amount is 0 (PDF:I.28.1/V0-DOM-006 - "effective
                // payable amounts" are zeroed), so showing it as-is would
                // render the line as free/invisible instead of at its real
                // price with the discount surfaced separately in
                // DiscountTotal (same "real price + separate discount line"
                // contract as BillItem.cs, V1-RMD-228). Reconstructed from
                // unit_price/quantity/tax_rate, the same inputs
                // OrderItem's own constructor derives GrossAmount from for
                // a non-discounted line; like the rest of this query, it
                // does not account for modifiers (gross_amount already
                // didn't for any status before this fix either).
                var lineTotal = status == "Complimentary"
                    ? Math.Round(unitPrice * quantity * (1 + reader.GetDecimal(6) / 100m), 2, MidpointRounding.AwayFromZero)
                    : storedGrossAmount;
                lines.Add(new CustomerDisplayLineDto(
                    reader.GetGuid(0), reader.GetString(1), quantity, unitPrice, lineTotal));
            }
        }

        var state = order.Status switch
        {
            "Completed" => "Completed",
            "Cancelled" or "Rejected" => "Idle",
            _ => "Active",
        };
        var message = order.Status switch
        {
            "Draft" => "Siparişiniz oluşturuluyor.",
            "Submitted" => "Siparişiniz alındı.",
            "Completed" => "Teşekkür ederiz.",
            _ => "Siparişiniz işleniyor.",
        };
        return new CustomerDisplaySnapshotDto(
            displayId, terminalId, orderId, order.Revision, state, order.Status == "Draft", order.Number, lines,
            order.Subtotal, order.Discount, order.Tax, order.Total, order.Currency,
            DateTimeOffset.UtcNow, message);
    }

    public async Task<Guid?> GetActiveDisplayIdAsync(Guid terminalId, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            SELECT display_id
            FROM customer_display.display_sessions
            WHERE terminal_id = @terminal_id AND revoked_at IS NULL AND expires_at > now()
            ORDER BY created_at DESC
            LIMIT 1;
            """);
        command.Parameters.AddWithValue("terminal_id", terminalId);
        return (Guid?)await command.ExecuteScalarAsync(cancellationToken);
    }

}
