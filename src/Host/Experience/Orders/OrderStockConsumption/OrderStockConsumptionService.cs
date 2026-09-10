using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.ModifierStock;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Orders.OrderAggregate;
using Npgsql;

namespace ALKAROS.Host.Experience.Orders.OrderStockConsumption;

/// <summary>
/// V1-RMD-143: when an order is accepted (Accept — regardless of channel:
/// Cashier/Waiter/NFC/QR) the stock of the sold products must really
/// decrease. Before this service, no channel went past
/// `PortionReservationStatus.NotApplicable`, and even the existing
/// `PortionReservation` lifecycle's own "Consumed" transition never
/// decremented `on_hand_quantity` (it only released the reservation hold)
/// — the real decrement happens through the same primitive Production
/// already consumes,
/// <see cref="IStockBalanceRepository.TryApplyGuardedOnHandDeltaAsync"/> +
/// a <see cref="StockMovementType.Consumption"/> movement (the same
/// pattern as `ProductionStockEffectService`; `StockMovementSourceType.
/// Order` already existed for exactly this, but had never been used).
/// A product can have one or more `inventory.product_stock_mappings` rows
/// (a real bill of materials/BOM) — all of them are consumed in a single
/// transaction; if even one hits insufficient stock, none are applied.
/// Semih's decision: if a product has no mapping at all, the accept is
/// rejected outright (never silently skipped) — no channel can accept an
/// order until a manager has mapped every product it sells.
///
/// Each movement's <c>sourceReferenceId</c> is the ORDER ITEM's own id, not
/// the order's — deliberately, so a later per-item reversal (a post-Accept
/// void that hasn't reached the kitchen yet, see `SentItemVoidStore`'s own
/// doc comment, V1-RMD-143 2026-09-09 follow-up) can find exactly the
/// movement(s) one specific line produced via
/// <see cref="IStockMovementRepository.GetBySourceAsync"/> without also
/// pulling in every other item's movements on the same order.
/// </summary>
public sealed class OrderStockConsumptionService
{
    private readonly IProductStockMappingRepository _mappings;
    private readonly IStockItemRepository _stockItems;
    private readonly IStockBalanceRepository _balances;
    private readonly IStockMovementRepository _movements;
    private readonly IModifierStockMappingRepository _modifierMappings;

    public OrderStockConsumptionService(
        IProductStockMappingRepository mappings,
        IStockItemRepository stockItems,
        IStockBalanceRepository balances,
        IStockMovementRepository movements,
        IModifierStockMappingRepository modifierMappings)
    {
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
        _stockItems = stockItems ?? throw new ArgumentNullException(nameof(stockItems));
        _balances = balances ?? throw new ArgumentNullException(nameof(balances));
        _movements = movements ?? throw new ArgumentNullException(nameof(movements));
        _modifierMappings = modifierMappings ?? throw new ArgumentNullException(nameof(modifierMappings));
    }

    /// <summary>
    /// Consumes stock for every item on <paramref name="order"/>, on the
    /// caller's own connection/transaction so it commits or rolls back
    /// atomically with the caller's own Accept write (same shape as
    /// Production/Purchasing's own direct-call edge into Inventory,
    /// V0-ARC-001 row 11). Throws <see cref="ProductStockNotConfiguredException"/>
    /// or <see cref="InsufficientOrderStockException"/> — both routine,
    /// expected outcomes a caller maps to a clear Turkish rejection — before
    /// touching any balance, so a failure on item 2 of 3 leaves item 1's
    /// (already-applied, same-transaction) delta rolled back along with it.
    /// </summary>
    public Task ConsumeForAcceptedOrderAsync(
        Order order,
        Guid actorId,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        return ConsumeItemsAsync(order, order.Items, "accepted", actorId, connection, transaction, cancellationToken);
    }

    /// <summary>
    /// V1-RMD-144: same consumption for an explicit subset of an order's
    /// items, used by the Cashier/Waiter submit path where only the lines
    /// activated by this particular submission may consume — a second round
    /// of items on the same table re-enters <see cref="Order.Submit"/> with
    /// the earlier lines already Active and already consumed.
    /// <paramref name="trigger"/> only names the moment in the movement's
    /// audit reason.
    /// </summary>
    public async Task ConsumeItemsAsync(
        Order order,
        IReadOnlyCollection<OrderItem> items,
        string trigger,
        Guid actorId,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger);

        foreach (var item in items)
        {
            // A waiter can void an item (ItemExceptionHandler.VoidItemAsync)
            // while the order still sits at PendingConfirmation — nothing
            // gates that on order.Status, only the item's own Status/
            // KitchenState — so a Cancelled line can reach here in the same
            // `order.Items` list. It was never sent to the kitchen and never
            // will be; consuming stock for it would charge inventory for a
            // product the customer no longer gets. Complimentary items are
            // still prepared and served for free, so they still consume.
            if (item.Status == OrderItemState.Cancelled)
                continue;

            var productMappings = await _mappings.GetByProductIdAsync(item.ProductId, cancellationToken);
            if (productMappings.Count == 0)
                throw new ProductStockNotConfiguredException(item.ProductId, item.ProductNameSnapshot);

            foreach (var mapping in productMappings)
            {
                var stockItem = await _stockItems.GetByIdAsync(mapping.StockItemId, cancellationToken)
                    ?? throw new StockItemNotFoundException(mapping.StockItemId);
                var locationId = stockItem.DefaultLocationId
                    ?? throw new StockItemHasNoDefaultLocationException(stockItem.Id, stockItem.Name);

                var consumeQuantity = item.Quantity * mapping.QuantityMultiplier;
                var applied = await _balances.TryApplyGuardedOnHandDeltaAsync(
                    mapping.StockItemId, locationId, -consumeQuantity, connection, transaction, cancellationToken);
                if (applied is null)
                {
                    throw new InsufficientOrderStockException(
                        item.ProductId, item.ProductNameSnapshot, stockItem.Name);
                }

                var movement = new StockMovement(
                    id: Guid.NewGuid(),
                    stockItemId: mapping.StockItemId,
                    stockLocationId: locationId,
                    movementType: StockMovementType.Consumption,
                    direction: MovementDirection.Out,
                    quantity: consumeQuantity,
                    unitCode: stockItem.TrackingUnitCode,
                    sourceType: StockMovementSourceType.Order,
                    sourceReferenceId: item.Id,
                    reason: $"Order {order.OrderNumber}, item {item.Id:D} ({item.ProductNameSnapshot}) {trigger}",
                    createdBy: actorId);
                await _movements.AppendAsync(movement, connection, transaction, cancellationToken);
            }

            await ConsumeModifiersAsync(order, item, trigger, actorId, connection, transaction, cancellationToken);
        }
    }

    /// <summary>
    /// V1-RMD-152: extras draw on the store room too — "ekstra peynir" is
    /// real cheese. The amount is V1-RMD-150's modifier quantity times the
    /// mapping multiplier, so the charge, the kitchen ticket and the depot
    /// all use one number.
    ///
    /// Unlike a product, a modifier with no mapping does NOT refuse the
    /// order. Most modifiers are an instruction rather than an ingredient
    /// (a cooking preference, an omission), and demanding a stock item for
    /// every free choice would bloat configuration for nothing. This is a
    /// deliberate business rule, not a swallowed failure.
    ///
    /// The movement's sourceReferenceId is the ORDER ITEM's id, exactly like
    /// the product's, so SentItemVoidStore's existing restore path gives
    /// these back with the rest of the line and needs no separate code.
    /// </summary>
    private async Task ConsumeModifiersAsync(
        Order order,
        OrderItem item,
        string trigger,
        Guid actorId,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (item.Modifiers.Count == 0)
            return;

        var modifierIds = item.Modifiers.Select(m => m.ModifierId).Distinct().ToArray();
        var mappings = await _modifierMappings.GetByModifierIdsAsync(modifierIds, cancellationToken);
        if (mappings.Count == 0)
            return;

        foreach (var modifier in item.Modifiers)
        {
            foreach (var mapping in mappings)
            {
                if (mapping.ModifierId != modifier.ModifierId)
                    continue;

                var stockItem = await _stockItems.GetByIdAsync(mapping.StockItemId, cancellationToken)
                    ?? throw new StockItemNotFoundException(mapping.StockItemId);
                var locationId = stockItem.DefaultLocationId
                    ?? throw new StockItemHasNoDefaultLocationException(stockItem.Id, stockItem.Name);

                var consumeQuantity = modifier.Quantity * mapping.QuantityMultiplier;
                var applied = await _balances.TryApplyGuardedOnHandDeltaAsync(
                    mapping.StockItemId, locationId, -consumeQuantity, connection, transaction, cancellationToken);
                if (applied is null)
                {
                    throw new InsufficientOrderStockException(
                        item.ProductId, modifier.ModifierNameSnapshot, stockItem.Name);
                }

                var movement = new StockMovement(
                    id: Guid.NewGuid(),
                    stockItemId: mapping.StockItemId,
                    stockLocationId: locationId,
                    movementType: StockMovementType.Consumption,
                    direction: MovementDirection.Out,
                    quantity: consumeQuantity,
                    unitCode: stockItem.TrackingUnitCode,
                    sourceType: StockMovementSourceType.Order,
                    sourceReferenceId: item.Id,
                    reason: $"Order {order.OrderNumber}, item {item.Id:D} modifier {modifier.ModifierNameSnapshot} {trigger}",
                    createdBy: actorId);
                await _movements.AppendAsync(movement, connection, transaction, cancellationToken);
            }
        }
    }
}

/// <summary>A sold product has no `inventory.product_stock_mappings` row at all — Semih's decision: this refuses acceptance outright, it is never silently skipped.</summary>
public sealed class ProductStockNotConfiguredException : Exception
{
    public Guid ProductId { get; }
    public string ProductName { get; }

    public ProductStockNotConfiguredException(Guid productId, string productName)
        : base($"Product '{productName}' ({productId}) has no stock mapping configured.")
    {
        ProductId = productId;
        ProductName = productName;
    }
}

/// <summary>One of a sold product's mapped stock items does not have enough on-hand quantity for this order's requested amount.</summary>
public sealed class InsufficientOrderStockException : Exception
{
    public Guid ProductId { get; }
    public string ProductName { get; }
    public string StockItemName { get; }

    public InsufficientOrderStockException(Guid productId, string productName, string stockItemName)
        : base($"Insufficient stock of '{stockItemName}' to accept product '{productName}' ({productId}).")
    {
        ProductId = productId;
        ProductName = productName;
        StockItemName = stockItemName;
    }
}

/// <summary>A stock item mapped from a product has no `default_location_id` — there is nowhere to actually apply the consumption delta.</summary>
public sealed class StockItemHasNoDefaultLocationException : Exception
{
    public Guid StockItemId { get; }
    public string StockItemName { get; }

    public StockItemHasNoDefaultLocationException(Guid stockItemId, string stockItemName)
        : base($"Stock item '{stockItemName}' ({stockItemId}) has no default location configured.")
    {
        StockItemId = stockItemId;
        StockItemName = stockItemName;
    }
}
