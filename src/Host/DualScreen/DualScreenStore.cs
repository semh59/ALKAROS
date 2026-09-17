using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.DualScreen;

public sealed partial class DualScreenStore
{
    public const int DefaultCatalogPageSize = 1000;
    public const int MaximumCatalogPageSize = 1000;
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int MaximumCatalogCursorLength = 1024;
    private static readonly TimeSpan PairingLifetime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DisplaySessionLifetime = TimeSpan.FromHours(12);
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresOrderRepository _orderRepository;

    public DualScreenStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        // Not DI-injected: DualScreenStore is registered by five different
        // Experience areas (Tables, Orders, Billing, Kitchen, OfflineReconciliation
        // — see their AddXxxExperience() TryAddSingleton<DualScreenStore>() calls)
        // purely for its cashier/display session authentication, none of which
        // compose the Orders module or its IOrderRepository registration. Building
        // the repository from the same NpgsqlDataSource this store already holds
        // (rather than requiring a fifth DI registration everywhere) keeps those
        // four unrelated compositions working unchanged.
        _orderRepository = new PostgresOrderRepository(dataSource);
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
                   p.name COLLATE "C", p.sku COLLATE "C",
                   p.prep_time_minutes,
                   remaining.remaining_count
            FROM catalog.products p
            LEFT JOIN catalog.categories c ON c.category_id = p.category_id AND c.active
            LEFT JOIN catalog.tax_profiles t ON t.tax_profile_id = p.tax_profile_id AND t.active
            -- V1-WTR-054: how many more of this product the mapped stock could
            -- still cover, mirroring OrderDtoAssembler.WithAvailableStockAsync's
            -- reasoning (V1-RMD-143) — the limiting stock item decides. NULL
            -- when the product has no mapping (unlimited, unchanged behavior).
            -- MIN() ignores NULL rows on its own, so a mapping with no default
            -- location or no balance row is silently excluded from the
            -- limiting decision, same as that C# loop's own "continue".
            LEFT JOIN LATERAL (
                SELECT MIN(FLOOR(sb.available_quantity / psm.quantity_multiplier))::int AS remaining_count
                FROM inventory.product_stock_mappings psm
                JOIN inventory.stock_items si ON si.id = psm.stock_item_id
                LEFT JOIN inventory.stock_balances sb
                    ON sb.stock_item_id = si.id AND sb.stock_location_id = si.default_location_id
                WHERE psm.product_id = p.product_id
            ) remaining ON true
            WHERE p.active
              AND p.is_available
              AND p.current_price IS NOT NULL
              -- 2026-09-16: a product whose mapped stock is exhausted is
              -- dropped from the catalog entirely, the same way an
              -- is_available=false product already is — never a
              -- remaining_count: 0 row for a client to render.
              AND (remaining.remaining_count IS NULL OR remaining.remaining_count > 0)
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
                    reader.GetDecimal(5), reader.GetDecimal(6),
                    PrepTimeMinutes: reader.IsDBNull(11) ? null : reader.GetInt32(11),
                    RemainingCount: reader.IsDBNull(12) ? null : reader.GetInt32(12)),
                reader.GetInt32(7), reader.GetInt32(8), reader.GetString(9), reader.GetString(10)));
        }

        var hasMore = rows.Count > limit;
        if (hasMore)
            rows.RemoveAt(rows.Count - 1);
        var nextCursor = hasMore ? EncodeCatalogCursor(rows[^1], normalizedCategory) : null;

        // V1-RMD-148: one query for the whole page's options, after the page
        // itself is settled — never one per product.
        var groups = await LoadModifierGroupsAsync(
            rows.Select(row => row.Product.ProductId).ToArray(), cancellationToken);
        var products = rows
            .Select(row => groups.TryGetValue(row.Product.ProductId, out var productGroups)
                ? row.Product with { ModifierGroups = productGroups }
                : row.Product)
            .ToArray();

        return new CatalogPage(products, nextCursor);
    }

    /// <summary>
    /// V1-RMD-148: every active option group of the given products, with the
    /// options inside them. A modifier belongs to a product either directly
    /// (<c>catalog.modifiers.product_id</c>) or through a group assigned to it
    /// (<c>catalog.product_modifier_groups</c>) — the same rule
    /// <c>OrderManagementStore.ResolveModifiersAsync</c> accepts on the way
    /// in, so a client can never be shown an option the order path would
    /// refuse.
    /// </summary>
    private async Task<Dictionary<Guid, List<CatalogModifierGroupDto>>> LoadModifierGroupsAsync(
        Guid[] productIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, List<CatalogModifierGroupDto>>();
        if (productIds.Length == 0)
            return result;

        await using var command = _dataSource.CreateCommand(
            """
            SELECT owner.product_id,
                   g.modifier_group_id, g.code, g.name, g.selection_type,
                   g.min_selections, g.max_selections,
                   m.modifier_id, m.code, m.name, m.price_delta
            FROM catalog.modifiers m
            JOIN catalog.modifier_groups g
              ON g.modifier_group_id = m.modifier_group_id AND g.active
            JOIN LATERAL (
                SELECT pmg.product_id
                FROM catalog.product_modifier_groups pmg
                WHERE pmg.modifier_group_id = g.modifier_group_id
                  AND pmg.product_id = ANY(@product_ids)
                UNION
                SELECT m.product_id
                WHERE m.product_id = ANY(@product_ids)
            ) AS owner ON TRUE
            WHERE m.active
            ORDER BY owner.product_id, g.code, m.code;
            """);
        command.Parameters.AddWithValue("product_ids", productIds);

        var builders = new Dictionary<(Guid ProductId, Guid GroupId), (CatalogModifierGroupDto Group, List<CatalogModifierDto> Modifiers)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var productId = reader.GetGuid(0);
            var groupId = reader.GetGuid(1);
            if (!builders.TryGetValue((productId, groupId), out var builder))
            {
                var modifiers = new List<CatalogModifierDto>();
                var group = new CatalogModifierGroupDto(
                    groupId,
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt16(4) == 1 ? "Single" : "Multiple",
                    reader.GetInt16(5),
                    reader.GetInt16(6),
                    modifiers);
                builder = (group, modifiers);
                builders[(productId, groupId)] = builder;

                if (!result.TryGetValue(productId, out var groupList))
                {
                    groupList = [];
                    result[productId] = groupList;
                }

                groupList.Add(group);
            }

            builder.Modifiers.Add(new CatalogModifierDto(
                reader.GetGuid(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetDecimal(10)));
        }

        return result;
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

        // V1-RMD-120: was a raw DELETE/UPDATE against orders.order_items plus
        // a hand-written net/tax/gross recompute and a separate
        // RecalculateOrderAsync that re-summed every item into orders.orders
        // — a second, independent implementation of Order.RemoveItem/
        // ChangeItemQuantity and the aggregate's own Subtotal/TaxTotal/Total
        // properties (found by an independent audit, 2026-09-07, boundary
        // wave). LockDraftOrderAsync above already holds the order's lock, so
        // this plain read cannot race with another mutator of the same order.
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken)
            ?? throw new DualScreenNotFoundException("Active order was not found for this terminal.");

        Order updated;
        try
        {
            updated = remove ? order.RemoveItem(itemId) : order.ChangeItemQuantity(itemId, quantity);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            // Unknown item, or an item no longer Draft (already Active/void) —
            // the raw SQL this replaces made no such distinction either
            // (DELETE/UPDATE affecting 0 rows for either reason).
            throw new DualScreenNotFoundException("Draft order item was not found.");
        }

        var revision = await _orderRepository.SaveAsync(updated, order.RowVersion, connection, transaction, cancellationToken);
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

    private static NpgsqlCommand CreateCommand(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }

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
