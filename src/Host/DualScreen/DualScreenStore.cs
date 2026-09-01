using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.Identity.DeviceSessions;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.DualScreen;

public sealed class DualScreenConflictException : Exception
{
    public DualScreenConflictException(string message) : base(message) { }
}

public sealed class DualScreenNotFoundException : Exception
{
    public DualScreenNotFoundException(string message) : base(message) { }
}

public sealed class DualScreenUnauthorizedException : Exception
{
    public DualScreenUnauthorizedException(string message) : base(message) { }
}

public sealed class DualScreenForbiddenException : Exception
{
    public DualScreenForbiddenException(string message) : base(message) { }
}

public sealed class DualScreenStore
{
    public const int DefaultCatalogPageSize = 1000;
    public const int MaximumCatalogPageSize = 1000;
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int MaximumCatalogCursorLength = 1024;
    private static readonly TimeSpan PairingLifetime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DisplaySessionLifetime = TimeSpan.FromHours(12);
    private readonly NpgsqlDataSource _dataSource;

    public DualScreenStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task CheckReadyAsync(CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand("SELECT 1;");
        if (!Equals(await command.ExecuteScalarAsync(cancellationToken), 1))
            throw new InvalidOperationException("Database readiness query returned an unexpected result.");
    }

    public async Task EnsureTerminalAsync(Guid terminalId, CancellationToken cancellationToken)
    {
        EnsureNotEmpty(terminalId, nameof(terminalId));
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO customer_display.terminals
                (terminal_id, active_order_id, row_version, created_at, updated_at)
            VALUES (@terminal_id, NULL, 1, now(), now())
            ON CONFLICT (terminal_id) DO NOTHING;
            """);
        command.Parameters.AddWithValue("terminal_id", terminalId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<CashierPrincipal?> AuthenticateCashierAsync(
        string? rawToken,
        Guid terminalId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || terminalId == Guid.Empty)
            return null;

        await using var command = _dataSource.CreateCommand(
            """
            SELECT s.session_id, s.user_id, u.display_name
            FROM identity.device_sessions s
            JOIN identity.users u ON u.user_id = s.user_id AND u.active
            WHERE s.token_hash = @token_hash
              AND s.device_id = @device_id
              AND s.revoked_at IS NULL
              AND s.expires_at > now();
            """);
        command.Parameters.AddWithValue("token_hash", DeviceSessionToken.Hash(rawToken));
        command.Parameters.AddWithValue("device_id", $"cashier:{terminalId:D}");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        return new CashierPrincipal(reader.GetGuid(1), terminalId, reader.GetGuid(0), reader.GetString(2));
    }

    /// <summary>
    /// Resolves a cashier session from the raw cookie token alone, deriving the
    /// bound terminal id from the session device id. Lets a client discover its
    /// terminal without carrying a hardcoded terminal id.
    /// </summary>
    public async Task<CashierPrincipal?> AuthenticateCashierByCookieAsync(
        string? rawToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return null;

        await using var command = _dataSource.CreateCommand(
            """
            SELECT s.session_id, s.user_id, u.display_name, s.device_id
            FROM identity.device_sessions s
            JOIN identity.users u ON u.user_id = s.user_id AND u.active
            WHERE s.token_hash = @token_hash
              AND s.device_id LIKE 'cashier:%'
              AND s.revoked_at IS NULL
              AND s.expires_at > now()
            LIMIT 1;
            """);
        command.Parameters.AddWithValue("token_hash", DeviceSessionToken.Hash(rawToken));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var deviceId = reader.GetString(3);
        if (!Guid.TryParse(deviceId["cashier:".Length..], out var terminalId))
            return null;

        return new CashierPrincipal(reader.GetGuid(1), terminalId, reader.GetGuid(0), reader.GetString(2));
    }

    public async Task<DisplayPrincipal?> AuthenticateDisplayAsync(
        string? rawToken,
        Guid? requiredDisplayId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return null;

        await using var command = _dataSource.CreateCommand(
            """
            UPDATE customer_display.display_sessions
            SET last_seen_at = now()
            WHERE token_hash = @token_hash
              AND revoked_at IS NULL
              AND expires_at > now()
              AND (@display_id IS NULL OR display_id = @display_id)
            RETURNING session_id, display_id, terminal_id, expires_at;
            """);
        command.Parameters.AddWithValue("token_hash", DualScreenToken.Hash(rawToken));
        command.Parameters.Add("display_id", NpgsqlDbType.Uuid).Value = requiredDisplayId ?? (object)DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        return new DisplayPrincipal(reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(0), reader.GetFieldValue<DateTimeOffset>(3));
    }

    public static int ParseCatalogLimit(string? value)
    {
        if (value is null)
            return DefaultCatalogPageSize;
        if (!int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var limit)
            || limit is < 1 or > MaximumCatalogPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, $"Catalog limit must be between 1 and {MaximumCatalogPageSize}.");
        }
        return limit;
    }

    public async Task<CatalogPage> GetCatalogAsync(
        string? categoryCode,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > MaximumCatalogPageSize)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, $"Catalog limit must be between 1 and {MaximumCatalogPageSize}.");

        var normalizedCategory = NormalizeCategoryCode(categoryCode);
        var decodedCursor = DecodeCatalogCursor(cursor, normalizedCategory);
        var rows = new List<CatalogRow>(limit + 1);
        await using var command = _dataSource.CreateCommand(
            """
            SELECT p.product_id, p.sku, p.name,
                   COALESCE(c.code, 'OTHER'), COALESCE(c.name, 'Diğer'),
                   p.current_price, COALESCE(t.vat_rate, 0),
                   COALESCE(c.sort_order, 2147483647), p.display_order,
                   p.name COLLATE "C", p.sku COLLATE "C"
            FROM catalog.products p
            LEFT JOIN catalog.categories c ON c.category_id = p.category_id AND c.active
            LEFT JOIN catalog.tax_profiles t ON t.tax_profile_id = p.tax_profile_id AND t.active
            WHERE p.active
              AND p.is_available
              AND p.current_price IS NOT NULL
              AND (@category_code IS NULL OR c.code = @category_code)
              AND (
                  NOT @has_cursor
                  OR (
                      COALESCE(c.sort_order, 2147483647),
                      p.display_order,
                      p.name COLLATE "C",
                      p.sku COLLATE "C",
                      p.product_id
                  ) > (
                      @cursor_category_sort,
                      @cursor_display_order,
                      @cursor_name,
                      @cursor_sku,
                      @cursor_product_id
                  )
              )
            ORDER BY COALESCE(c.sort_order, 2147483647),
                     p.display_order,
                     p.name COLLATE "C",
                     p.sku COLLATE "C",
                     p.product_id
            LIMIT @fetch_limit;
            """);
        command.Parameters.Add("category_code", NpgsqlDbType.Varchar).Value = normalizedCategory ?? (object)DBNull.Value;
        command.Parameters.AddWithValue("has_cursor", decodedCursor is not null);
        command.Parameters.AddWithValue("cursor_category_sort", decodedCursor?.CategorySort ?? int.MinValue);
        command.Parameters.AddWithValue("cursor_display_order", decodedCursor?.DisplayOrder ?? int.MinValue);
        command.Parameters.AddWithValue("cursor_name", decodedCursor?.Name ?? string.Empty);
        command.Parameters.AddWithValue("cursor_sku", decodedCursor?.Sku ?? string.Empty);
        command.Parameters.AddWithValue("cursor_product_id", decodedCursor?.ProductId ?? Guid.Empty);
        command.Parameters.AddWithValue("fetch_limit", limit + 1);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CatalogRow(
                new CatalogProductDto(
                    reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                    reader.GetDecimal(5), reader.GetDecimal(6)),
                reader.GetInt32(7), reader.GetInt32(8), reader.GetString(9), reader.GetString(10)));
        }

        var hasMore = rows.Count > limit;
        if (hasMore)
            rows.RemoveAt(rows.Count - 1);
        var nextCursor = hasMore ? EncodeCatalogCursor(rows[^1], normalizedCategory) : null;
        return new CatalogPage(rows.Select(row => row.Product).ToArray(), nextCursor);
    }

    public Task<StartOrderResponse> StartOrderAsync(Guid terminalId, CancellationToken cancellationToken)
        => StartOrderAsync(terminalId, new StartOrderRequest(), cancellationToken);

    public async Task<StartOrderResponse> StartOrderAsync(
        Guid terminalId,
        StartOrderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TableId is null && request.ExpectedTableRowVersion is not null)
            throw new ArgumentException("A table row version requires a table id.", nameof(request));
        if (request.ExpectedTableRowVersion is <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Expected table row version must be positive.");

        await EnsureTerminalAsync(terminalId, cancellationToken);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        Guid? activeOrderId;
        await using (var lockTerminal = CreateCommand(connection, transaction,
            "SELECT active_order_id FROM customer_display.terminals WHERE terminal_id = @terminal_id FOR UPDATE;"))
        {
            lockTerminal.Parameters.AddWithValue("terminal_id", terminalId);
            var result = await lockTerminal.ExecuteScalarAsync(cancellationToken);
            activeOrderId = result is null or DBNull ? null : (Guid)result;
        }

        if (activeOrderId is { } existingId)
        {
            await using var statusCommand = CreateCommand(connection, transaction,
                "SELECT status FROM orders.orders WHERE order_id = @order_id;");
            statusCommand.Parameters.AddWithValue("order_id", existingId);
            var status = (string?)await statusCommand.ExecuteScalarAsync(cancellationToken);
            if (status is not ("Submitted" or "Completed" or "Cancelled" or "Rejected"))
                throw new DualScreenConflictException("Terminal already has an active order.");
        }

        if (request.TableId is { } tableId)
        {
            string tableStatus;
            bool tableActive;
            Guid? tableOrderId;
            Guid? tableBillId;
            await using (var lockTable = CreateCommand(connection, transaction,
                """
                SELECT active, current_status, current_order_id, current_bill_id
                FROM table_mgmt.tables
                WHERE table_id = @table_id
                FOR UPDATE;
                """))
            {
                lockTable.Parameters.AddWithValue("table_id", tableId);
                await using var reader = await lockTable.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    throw new DualScreenNotFoundException("Table was not found.");
                tableActive = reader.GetBoolean(0);
                tableStatus = reader.GetString(1);
                tableOrderId = reader.IsDBNull(2) ? null : reader.GetGuid(2);
                tableBillId = reader.IsDBNull(3) ? null : reader.GetGuid(3);
            }

            // ExpectedTableRowVersion is accepted for API compatibility but is no
            // longer a gate (V1-RMD-090). The row is held FOR UPDATE and the
            // decision to seat is made from the fresh state below: an inactive
            // table, an open bill, an existing non-editable order, an order active
            // on another terminal, or a non-Available status each reject with a
            // specific error, and the final bind UPDATE re-checks
            // (current_status = 'Available' AND current_order_id IS NULL AND
            // current_bill_id IS NULL) atomically. A stale version on an otherwise
            // seatable table no longer forces a spurious "refresh".
            if (!tableActive)
                throw new DualScreenConflictException("Inactive tables cannot receive an order.");
            if (tableBillId is not null)
                throw new DualScreenConflictException("Table already has an active bill.");

            if (tableOrderId is { } existingTableOrderId)
            {
                string existingStatus;
                string existingOrderNumber;
                long existingRevision;
                Guid? existingOrderTableId;
                await using (var lockOrder = CreateCommand(connection, transaction,
                    """
                    SELECT status, order_number, row_version, table_id
                    FROM orders.orders
                    WHERE order_id = @order_id
                    FOR UPDATE;
                    """))
                {
                    lockOrder.Parameters.AddWithValue("order_id", existingTableOrderId);
                    await using var reader = await lockOrder.ExecuteReaderAsync(cancellationToken);
                    if (!await reader.ReadAsync(cancellationToken))
                        throw new DualScreenConflictException("Table order pointer is stale.");
                    existingStatus = reader.GetString(0);
                    existingOrderNumber = reader.GetString(1);
                    existingRevision = reader.GetInt64(2);
                    existingOrderTableId = reader.IsDBNull(3) ? null : reader.GetGuid(3);
                }

                if (existingOrderTableId != tableId || existingStatus != "Draft")
                    throw new DualScreenConflictException("Table already has a non-editable order.");

                await using (var activeTerminal = CreateCommand(connection, transaction,
                    """
                    SELECT EXISTS(
                        SELECT 1
                        FROM customer_display.terminals
                        WHERE active_order_id = @order_id AND terminal_id <> @terminal_id);
                    """))
                {
                    activeTerminal.Parameters.AddWithValue("order_id", existingTableOrderId);
                    activeTerminal.Parameters.AddWithValue("terminal_id", terminalId);
                    if ((bool)(await activeTerminal.ExecuteScalarAsync(cancellationToken) ?? false))
                        throw new DualScreenConflictException("Table order is active on another terminal.");
                }

                await using (var bindExisting = CreateCommand(connection, transaction,
                    """
                    UPDATE customer_display.terminals
                    SET active_order_id = @order_id, row_version = row_version + 1, updated_at = now()
                    WHERE terminal_id = @terminal_id;
                    """))
                {
                    bindExisting.Parameters.AddWithValue("order_id", existingTableOrderId);
                    bindExisting.Parameters.AddWithValue("terminal_id", terminalId);
                    await bindExisting.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                return new StartOrderResponse(existingTableOrderId, existingOrderNumber, existingRevision);
            }

            if (tableStatus != "Available")
                throw new DualScreenConflictException("Only an available table can receive a new order.");
        }

        var orderId = Guid.NewGuid();
        var orderNumber = $"POS-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{orderId:N}"[..32].ToUpperInvariant();
        await using (var insertOrder = CreateCommand(connection, transaction,
            """
            INSERT INTO orders.orders (
                order_id, source, source_reference_id, source_external_id, table_id, customer_id,
                status, confirmation_status, order_number, notes,
                subtotal, discount_total, tax_total, total, currency_code,
                submitted_at, accepted_at, closed_at, cancelled_at,
                created_at, updated_at, row_version)
            VALUES (
                @order_id, 'Cashier', NULL, NULL, @table_id, NULL,
                'Draft', 'NotRequired', @order_number, NULL,
                0, 0, 0, 0, 'TRY',
                NULL, NULL, NULL, NULL,
                now(), now(), 1);
            """))
        {
            insertOrder.Parameters.AddWithValue("order_id", orderId);
            insertOrder.Parameters.AddWithValue("order_number", orderNumber);
            insertOrder.Parameters.AddWithValue("table_id", request.TableId ?? (object)DBNull.Value);
            await insertOrder.ExecuteNonQueryAsync(cancellationToken);
        }

        if (request.TableId is { } createdTableId)
        {
            await using var bindTable = CreateCommand(connection, transaction,
                """
                UPDATE table_mgmt.tables
                SET current_order_id = @order_id,
                    current_status = 'Occupied',
                    row_version = row_version + 1
                WHERE table_id = @table_id
                  AND current_status = 'Available'
                  AND current_order_id IS NULL
                  AND current_bill_id IS NULL;
                """);
            bindTable.Parameters.AddWithValue("order_id", orderId);
            bindTable.Parameters.AddWithValue("table_id", createdTableId);
            if (await bindTable.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new DualScreenConflictException("Table changed before the order could be bound.");
        }

        await using (var bindTerminal = CreateCommand(connection, transaction,
            """
            UPDATE customer_display.terminals
            SET active_order_id = @order_id, row_version = row_version + 1, updated_at = now()
            WHERE terminal_id = @terminal_id;
            """))
        {
            bindTerminal.Parameters.AddWithValue("order_id", orderId);
            bindTerminal.Parameters.AddWithValue("terminal_id", terminalId);
            await bindTerminal.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new StartOrderResponse(orderId, orderNumber, 1);
    }

    public async Task<OrderMutationResult> AddItemAsync(
        Guid terminalId,
        Guid orderId,
        AddOrderItemRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0 || request.Quantity > 999)
            throw new ArgumentOutOfRangeException(nameof(request), request.Quantity, "Quantity must be between 0 and 999.");

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockDraftOrderAsync(connection, transaction, terminalId, orderId, request.ExpectedRevision, cancellationToken);

        ProductRow product;
        await using (var productCommand = CreateCommand(connection, transaction,
            """
            SELECT p.sku, p.name, p.current_price, COALESCE(t.vat_rate, 0)
            FROM catalog.products p
            LEFT JOIN catalog.tax_profiles t ON t.tax_profile_id = p.tax_profile_id AND t.active
            WHERE p.product_id = @product_id AND p.active AND p.is_available AND p.current_price IS NOT NULL;
            """))
        {
            productCommand.Parameters.AddWithValue("product_id", request.ProductId);
            await using var reader = await productCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new DualScreenNotFoundException("Product was not found or has no active price.");
            product = new ProductRow(reader.GetString(0), reader.GetString(1), reader.GetDecimal(2), reader.GetDecimal(3));
        }

        Guid? existingItemId;
        decimal existingQuantity;
        await using (var existingCommand = CreateCommand(connection, transaction,
            """
            SELECT order_item_id, quantity
            FROM orders.order_items
            WHERE order_id = @order_id AND product_id = @product_id AND status = 'Draft'
            ORDER BY created_at
            LIMIT 1
            FOR UPDATE;
            """))
        {
            existingCommand.Parameters.AddWithValue("order_id", orderId);
            existingCommand.Parameters.AddWithValue("product_id", request.ProductId);
            await using var reader = await existingCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                existingItemId = reader.GetGuid(0);
                existingQuantity = reader.GetDecimal(1);
            }
            else
            {
                existingItemId = null;
                existingQuantity = 0;
            }
        }

        var quantity = existingQuantity + request.Quantity;
        if (quantity > 999)
            throw new ArgumentOutOfRangeException(nameof(request), quantity, "Cumulative quantity must not exceed 999.");

        var net = RoundCurrency(product.UnitPrice * quantity);
        var tax = RoundCurrency(net * product.TaxRate / 100m);
        var gross = RoundCurrency(net + tax);

        if (existingItemId is { } itemId)
        {
            await using var update = CreateCommand(connection, transaction,
                """
                UPDATE orders.order_items
                SET quantity = @quantity, net_amount = @net, tax_amount = @tax, gross_amount = @gross,
                    updated_at = now(), row_version = row_version + 1
                WHERE order_item_id = @item_id;
                """);
            update.Parameters.AddWithValue("item_id", itemId);
            BindAmounts(update, quantity, net, tax, gross);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            await using var insert = CreateCommand(connection, transaction,
                """
                INSERT INTO orders.order_items (
                    order_item_id, order_id, product_id, product_name_snapshot, sku_snapshot,
                    quantity, unit_price, discount_amount, tax_rate, tax_amount, net_amount, gross_amount,
                    status, kitchen_state, portion_reservation_status, notes, created_at, updated_at, row_version)
                VALUES (
                    @item_id, @order_id, @product_id, @name, @sku,
                    @quantity, @unit_price, 0, @tax_rate, @tax, @net, @gross,
                    'Draft', 'NotSent', 'NotApplicable', NULL, now(), now(), 1);
                """);
            insert.Parameters.AddWithValue("item_id", Guid.NewGuid());
            insert.Parameters.AddWithValue("order_id", orderId);
            insert.Parameters.AddWithValue("product_id", request.ProductId);
            insert.Parameters.AddWithValue("name", product.Name);
            insert.Parameters.AddWithValue("sku", product.Sku);
            insert.Parameters.AddWithValue("unit_price", product.UnitPrice);
            insert.Parameters.AddWithValue("tax_rate", product.TaxRate);
            BindAmounts(insert, quantity, net, tax, gross);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        var revision = await RecalculateOrderAsync(connection, transaction, orderId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new OrderMutationResult(orderId, revision);
    }

    public Task<OrderMutationResult> ChangeItemQuantityAsync(
        Guid terminalId,
        Guid orderId,
        Guid itemId,
        ChangeOrderItemQuantityRequest request,
        CancellationToken cancellationToken)
        => MutateExistingItemAsync(terminalId, orderId, itemId, request.ExpectedRevision, request.Quantity, false, cancellationToken);

    public Task<OrderMutationResult> RemoveItemAsync(
        Guid terminalId,
        Guid orderId,
        Guid itemId,
        long expectedRevision,
        CancellationToken cancellationToken)
        => MutateExistingItemAsync(terminalId, orderId, itemId, expectedRevision, 0, true, cancellationToken);

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
            SELECT order_item_id, product_name_snapshot, quantity, unit_price, gross_amount
            FROM orders.order_items
            WHERE order_id = @order_id AND status IN ('Draft', 'Active')
            ORDER BY created_at, order_item_id;
            """))
        {
            lineCommand.Parameters.AddWithValue("order_id", orderId.Value);
            await using var reader = await lineCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                lines.Add(new CustomerDisplayLineDto(
                    reader.GetGuid(0), reader.GetString(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4)));
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

    private async Task<OrderMutationResult> MutateExistingItemAsync(
        Guid terminalId,
        Guid orderId,
        Guid itemId,
        long expectedRevision,
        decimal quantity,
        bool remove,
        CancellationToken cancellationToken)
    {
        if (!remove && (quantity <= 0 || quantity > 999))
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be between 0 and 999.");

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await LockDraftOrderAsync(connection, transaction, terminalId, orderId, expectedRevision, cancellationToken);

        int affected;
        if (remove)
        {
            await using var delete = CreateCommand(connection, transaction,
                "DELETE FROM orders.order_items WHERE order_item_id = @item_id AND order_id = @order_id AND status = 'Draft';");
            delete.Parameters.AddWithValue("item_id", itemId);
            delete.Parameters.AddWithValue("order_id", orderId);
            affected = await delete.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            await using var update = CreateCommand(connection, transaction,
                """
                UPDATE orders.order_items
                SET quantity = @quantity,
                    net_amount = round(unit_price * @quantity - discount_amount, 2),
                    tax_amount = round((unit_price * @quantity - discount_amount) * tax_rate / 100, 2),
                    gross_amount = round(unit_price * @quantity - discount_amount, 2)
                        + round((unit_price * @quantity - discount_amount) * tax_rate / 100, 2),
                    updated_at = now(), row_version = row_version + 1
                WHERE order_item_id = @item_id AND order_id = @order_id AND status = 'Draft';
                """);
            update.Parameters.AddWithValue("quantity", quantity);
            update.Parameters.AddWithValue("item_id", itemId);
            update.Parameters.AddWithValue("order_id", orderId);
            affected = await update.ExecuteNonQueryAsync(cancellationToken);
        }

        if (affected == 0)
            throw new DualScreenNotFoundException("Draft order item was not found.");

        var revision = await RecalculateOrderAsync(connection, transaction, orderId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new OrderMutationResult(orderId, revision);
    }

    private static async Task LockDraftOrderAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid terminalId,
        Guid orderId,
        long expectedRevision,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            """
            SELECT o.status, o.row_version
            FROM customer_display.terminals t
            JOIN orders.orders o ON o.order_id = t.active_order_id
            WHERE t.terminal_id = @terminal_id AND o.order_id = @order_id
            FOR UPDATE OF t, o;
            """);
        command.Parameters.AddWithValue("terminal_id", terminalId);
        command.Parameters.AddWithValue("order_id", orderId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new DualScreenNotFoundException("Active order was not found for this terminal.");
        if (!string.Equals(reader.GetString(0), "Draft", StringComparison.Ordinal))
            throw new DualScreenConflictException("Only a Draft order can be edited.");
        var actualRevision = reader.GetInt64(1);
        if (actualRevision != expectedRevision)
            throw new DualScreenConflictException($"Order revision is stale. Expected {expectedRevision}, actual {actualRevision}.");
    }

    private static async Task<long> RecalculateOrderAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction,
            """
            UPDATE orders.orders o
            SET subtotal = totals.subtotal,
                discount_total = totals.discount_total,
                tax_total = totals.tax_total,
                total = totals.total,
                updated_at = now(),
                row_version = o.row_version + 1
            FROM (
                SELECT COALESCE(sum(net_amount + discount_amount), 0) AS subtotal,
                       COALESCE(sum(discount_amount), 0) AS discount_total,
                       COALESCE(sum(tax_amount), 0) AS tax_total,
                       COALESCE(sum(gross_amount), 0) AS total
                FROM orders.order_items
                WHERE order_id = @order_id AND status IN ('Draft', 'Active')
            ) totals
            WHERE o.order_id = @order_id
            RETURNING o.row_version;
            """);
        command.Parameters.AddWithValue("order_id", orderId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new DualScreenNotFoundException("Order was not found."));
    }

    private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }

    private static void BindAmounts(NpgsqlCommand command, decimal quantity, decimal net, decimal tax, decimal gross)
    {
        command.Parameters.AddWithValue("quantity", quantity);
        command.Parameters.AddWithValue("net", net);
        command.Parameters.AddWithValue("tax", tax);
        command.Parameters.AddWithValue("gross", gross);
    }

    private static decimal RoundCurrency(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string? NormalizeCategoryCode(string? value)
    {
        if (value is null)
            return null;
        var normalized = value.Trim();
        if (normalized.Length is < 1 or > 50)
            throw new ArgumentException("Category code must contain between 1 and 50 characters.", nameof(value));
        return normalized;
    }

    private static CatalogCursor? DecodeCatalogCursor(string? value, string? categoryCode)
    {
        if (value is null)
            return null;
        if (value.Length is < 1 or > MaximumCatalogCursorLength)
            throw new ArgumentException("Catalog cursor length is invalid.", nameof(value));

        try
        {
            var base64 = value.Replace('-', '+').Replace('_', '/');
            base64 += (base64.Length % 4) switch
            {
                0 => string.Empty,
                2 => "==",
                3 => "=",
                _ => throw new FormatException("Catalog cursor padding is invalid."),
            };
            var decoded = JsonSerializer.Deserialize<CatalogCursor>(Convert.FromBase64String(base64))
                ?? throw new JsonException("Catalog cursor payload is empty.");
            if (decoded.ProductId == Guid.Empty
                || string.IsNullOrEmpty(decoded.Name)
                || decoded.Name.Length > 300
                || string.IsNullOrEmpty(decoded.Sku)
                || decoded.Sku.Length > 100
                || !string.Equals(decoded.CategoryCode, categoryCode, StringComparison.Ordinal))
            {
                throw new JsonException("Catalog cursor payload is invalid.");
            }
            return decoded;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new ArgumentException("Catalog cursor is invalid.", nameof(value), exception);
        }
    }

    private static string EncodeCatalogCursor(CatalogRow row, string? categoryCode)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new CatalogCursor(
            row.CategorySort, row.DisplayOrder, row.Name, row.Sku, row.Product.ProductId, categoryCode));
        return Convert.ToBase64String(payload).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string CreatePairingCode()
    {
        Span<char> chars = stackalloc char[8];
        for (var index = 0; index < chars.Length; index++)
            chars[index] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return new string(chars);
    }

    private static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Pairing code cannot be empty.", nameof(code));
        var normalized = code.Trim().Replace("-", "", StringComparison.Ordinal).ToUpperInvariant();
        if (normalized.Length != 8 || normalized.Any(character => !CodeAlphabet.Contains(character)))
            throw new ArgumentException("Pairing code must contain eight supported characters.", nameof(code));
        return normalized;
    }

    private static void EnsureNotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
    }

    private static CustomerDisplaySnapshotDto IdleSnapshot(Guid displayId, Guid terminalId)
        => new(
            displayId, terminalId, null, 0, "Idle", false, null, [], 0, 0, 0, 0, "TRY",
            DateTimeOffset.UtcNow, "Sıradaki işlem bekleniyor.");

    private sealed record ProductRow(string Sku, string Name, decimal UnitPrice, decimal TaxRate);
    private sealed record CatalogRow(
        CatalogProductDto Product,
        int CategorySort,
        int DisplayOrder,
        string Name,
        string Sku);
    private sealed record CatalogCursor(
        int CategorySort,
        int DisplayOrder,
        string Name,
        string Sku,
        Guid ProductId,
        string? CategoryCode);
    private sealed record PairingRow(
        Guid DisplayId,
        Guid? TerminalId,
        string SecretHash,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? ApprovedAt,
        DateTimeOffset? ConsumedAt,
        short FailedAttempts);
    private sealed record OrderRow(
        string Number,
        string Status,
        decimal Subtotal,
        decimal Discount,
        decimal Tax,
        decimal Total,
        string Currency,
        long Revision);
}
