using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.Experience.Orders;

/// <summary>
/// Refactor step 1/7 (docs/engineering/garson-refactor-plan.md, 2026-09-12):
/// the "how do we turn an <see cref="Order"/> into an <see cref="OrderDto"/>,
/// with its available-stock enrichment" question, extracted out of the
/// former god-class <c>OrderManagementStore</c> so every order-reading
/// surface (table-draft create/merge, plain order lookup, table lookup, the
/// NFC channel) shares exactly one implementation instead of each carrying
/// its own copy that can drift from the others — the exact class of bug
/// V1-RMD-147's own comment on <see cref="MapModifiers"/> already flagged
/// as the reason that one projection is shared.
/// </summary>
public sealed class OrderDtoAssembler
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IOrderRepository _repository;
    private readonly IProductStockMappingRepository _stockMappings;
    private readonly IStockItemRepository _stockItems;
    private readonly IStockBalanceRepository _stockBalances;

    public OrderDtoAssembler(
        NpgsqlDataSource dataSource,
        IOrderRepository repository,
        IProductStockMappingRepository stockMappings,
        IStockItemRepository stockItems,
        IStockBalanceRepository stockBalances)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _stockMappings = stockMappings ?? throw new ArgumentNullException(nameof(stockMappings));
        _stockItems = stockItems ?? throw new ArgumentNullException(nameof(stockItems));
        _stockBalances = stockBalances ?? throw new ArgumentNullException(nameof(stockBalances));
    }

    /// <summary>
    /// Semih's own "kalan stok bilgisi ver garsona" (2026-09-09): a staff
    /// member viewing an order — typically a PendingConfirmation one, right
    /// before deciding Accept/Reject — sees how many more units of each item
    /// the mapped stock item(s) could still cover, the exact same
    /// availableQuantity/quantityMultiplier arithmetic
    /// StockMasterEndpoints exposes per product. Null when the product has
    /// no stock mapping at all yet (not tracked) rather than a misleading
    /// zero; this is purely a display aid, it does not gate anything —
    /// OrderStockConsumptionService is the real, authoritative check that
    /// runs at Accept time.
    /// </summary>
    public async Task<OrderDto> WithAvailableStockAsync(OrderDto dto, CancellationToken cancellationToken)
    {
        // V1-RMD-156: this used to issue up to three round trips PER LINE
        // (mappings, then the stock item, then the balance, for every mapping
        // a line had) on a path every order-viewing call goes through —
        // GetOrderByIdAsync, table-draft's own response, and this same
        // method reused for GetOrderByIdAsync too. Three batched queries
        // now cover every line in the order regardless of how many it has.
        if (dto.Items.Count == 0) return dto;

        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToArray();
        var mappings = await _stockMappings.GetByProductIdsAsync(productIds, cancellationToken);
        if (mappings.Count == 0) return dto;

        var mappingsByProduct = mappings
            .GroupBy(m => m.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var stockItemIds = mappings.Select(m => m.StockItemId).Distinct().ToArray();
        var stockItems = await _stockItems.GetByIdsAsync(stockItemIds, cancellationToken);
        var stockItemsById = stockItems.ToDictionary(s => s.Id);

        var locatedStockItemIds = stockItems
            .Where(s => s.DefaultLocationId is not null)
            .Select(s => s.Id)
            .ToArray();
        var balances = locatedStockItemIds.Length == 0
            ? []
            : await _stockBalances.GetByStockItemsAsync(locatedStockItemIds, cancellationToken);
        // stock_balances is unique per (stock_item_id, stock_location_id), not
        // per stock_item_id alone — a stock item CAN carry balance rows at
        // several locations. The single-pair lookup this replaces only ever
        // asked about one location, the item's own default, so the batch
        // result is keyed the same way: (item, its default location).
        var balanceByStockItem = balances
            .Where(b => stockItemsById.TryGetValue(b.StockItemId, out var stockItem)
                        && stockItem.DefaultLocationId == b.StockLocationId)
            .ToDictionary(b => b.StockItemId);

        var enrichedItems = new List<OrderItemDto>(dto.Items.Count);
        foreach (var item in dto.Items)
        {
            decimal? availableStockQuantity = null;
            if (mappingsByProduct.TryGetValue(item.ProductId, out var productMappings))
            {
                foreach (var mapping in productMappings)
                {
                    if (!stockItemsById.TryGetValue(mapping.StockItemId, out var stockItem)
                        || stockItem.DefaultLocationId is null)
                        continue;

                    if (!balanceByStockItem.TryGetValue(mapping.StockItemId, out var balance))
                        continue;

                    // The limiting stock item decides how many more units of
                    // the product can still be made — same reasoning as a
                    // real BOM.
                    var unitsFromThisMapping = balance.AvailableQuantity / mapping.QuantityMultiplier;
                    availableStockQuantity = availableStockQuantity is null
                        ? unitsFromThisMapping
                        : Math.Min(availableStockQuantity.Value, unitsFromThisMapping);
                }
            }

            enrichedItems.Add(item with { AvailableStockQuantity = availableStockQuantity });
        }

        return dto with { Items = enrichedItems };
    }

    public async Task<OrderDto?> LoadOrderDtoAsync(Guid orderId, Guid tableId, CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(orderId, cancellationToken);
        if (order == null) return null;

        var tableNumber = await GetTableNumberAsync(tableId, cancellationToken) ?? "—";
        var dto = MapToDto(order, tableNumber);
        // V1-RMD-143: shared with GetOrderByIdAsync so every order-viewing
        // route (GET /table/{tableId}, the table-draft create/replay
        // responses, and GET /{orderId}) shows the same "kalan stok" —
        // a waiter looking up an order by its table, the far more common
        // path on the floor, must not be the one view left without it.
        return await WithAvailableStockAsync(dto, cancellationToken);
    }

    public async Task<string?> GetTableNumberAsync(Guid? tableId, CancellationToken cancellationToken)
    {
        if (tableId == null) return null;
        await using var cmd = _dataSource.CreateCommand(
            "SELECT table_number FROM table_mgmt.tables WHERE table_id = @table_id;");
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId.Value;
        var res = await cmd.ExecuteScalarAsync(cancellationToken);
        return res as string;
    }

    /// <summary>Same lookup as the connection/transaction overload below, on
    /// this assembler's own connection — used after a failed transaction has
    /// already been rolled back and cannot be reused.</summary>
    public async Task<Guid?> FindOrderIdBySubmissionAsync(Guid tableId, Guid submissionId, CancellationToken cancellationToken)
    {
        await using var cmd = _dataSource.CreateCommand(
            """
            SELECT order_id
            FROM orders.orders
            WHERE table_id = @table_id AND source_reference_id = @submission_id
            LIMIT 1;
            """);
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        cmd.Parameters.Add("submission_id", NpgsqlDbType.Uuid).Value = submissionId;
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is Guid orderId ? orderId : null;
    }

    /// <summary>
    /// V1-RMD-123: looks up an existing order for the table by its
    /// client-generated submission id (see <see cref="CreateTableDraftRequest.Id"/>),
    /// regardless of the order's current status — a plain read, not FOR
    /// UPDATE, since the real serialization guard against a concurrent
    /// duplicate is the database's own partial unique index, not this check.
    /// </summary>
    public static async Task<Guid?> FindOrderIdBySubmissionAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tableId, Guid submissionId, CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT order_id
            FROM orders.orders
            WHERE table_id = @table_id AND source_reference_id = @submission_id
            LIMIT 1;
            """, connection, transaction);
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        cmd.Parameters.Add("submission_id", NpgsqlDbType.Uuid).Value = submissionId;
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is Guid orderId ? orderId : null;
    }

    /// <summary>
    /// V1-ORD-006: the check currently attached to this table, which is
    /// exactly what <c>table_mgmt.tables.current_order_id</c> points at.
    /// Sending a check to the cashier clears that pointer, so "attached" and
    /// "not yet sent to the cashier" are the same condition and need no extra
    /// column.
    /// </summary>
    /// <remarks>
    /// This used to match <c>status = 'Draft'</c>. The waiter client never
    /// leaves an order in Draft — it always draft-then-submits in one go — so
    /// every round after the first missed this lookup and opened a *second*
    /// order on the table, while the read path returned only the newest one.
    /// A party ordering ₺400 of starters and then ₺900 of mains showed ₺900
    /// on the bill and on the table tile, and the ₺400 was never billed.
    /// </remarks>
    public static async Task<OrderDto?> GetActiveOrderByTableIdInternalAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tableId, CancellationToken cancellationToken)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT o.order_id, o.status, o.row_version, o.created_at
            FROM orders.orders o
            JOIN table_mgmt.tables t ON t.current_order_id = o.order_id
            WHERE t.table_id = @table_id
              AND o.status IN ('Draft', 'Submitted')
            FOR UPDATE OF o;
            """, connection, transaction);
        cmd.Parameters.Add("table_id", NpgsqlDbType.Uuid).Value = tableId;
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new OrderDto(
            reader.GetGuid(0),
            tableId,
            "",
            reader.GetString(1),
            reader.GetInt64(2),
            0,
            [],
            reader.GetFieldValue<DateTimeOffset>(3));
    }

    public static OrderDto MapToDto(Order order, string tableNumber)
    {
        var dtos = order.Items.Select(i => new OrderItemDto(
            i.Id,
            i.ProductId,
            i.ProductNameSnapshot,
            i.Quantity,
            i.UnitPrice,
            i.GrossAmount,
            i.Notes,
            AvailableStockQuantity: null,
            Status: i.Status.ToString(),
            KitchenState: i.KitchenState.ToString(),
            CreatedAt: i.CreatedAt,
            Modifiers: MapModifiers(i),
            // V1-RMD-168: mirrors ItemExceptionHandler.VoidItemAsync's own
            // eligibility check exactly.
            CanVoid: i.Status is OrderItemState.Active or OrderItemState.Draft
                && i.KitchenState == KitchenState.NotSent,
            // V1-RMD-168: mirrors SentItemVoidStore.VoidAsync's own
            // eligibility check exactly.
            CanVoidSent: i.Status == OrderItemState.Active
                && i.KitchenState != KitchenState.NotSent
                && i.KitchenState is not (KitchenState.Served or KitchenState.Cancelled),
            // V1-RMD-177: mirrors ItemExceptionHandler.ApplyComplimentaryAsync's
            // own eligibility check exactly.
            CanComp: i.Status == OrderItemState.Active,
            SeatId: i.SeatId,
            CourseNumber: i.CourseNumber
        )).ToList();

        return new OrderDto(
            order.Id,
            order.TableId ?? Guid.Empty,
            tableNumber,
            order.Status.ToString(),
            order.RowVersion,
            order.Total,
            dtos,
            order.CreatedAt,
            order.PartySize
        );
    }

    /// <summary>
    /// V1-RMD-147: projects an item's recorded modifiers. Shared by every
    /// order-reading surface (table-draft, plain order lookup, NFC) so a
    /// line looks the same whichever one served it.
    /// </summary>
    public static IReadOnlyList<OrderItemModifierDto>? MapModifiers(OrderItem item)
        => item.Modifiers.Count == 0
            ? null
            : item.Modifiers
                .Select(m => new OrderItemModifierDto(m.ModifierId, m.ModifierNameSnapshot, m.PriceDelta, m.Quantity))
                .ToList();
}
