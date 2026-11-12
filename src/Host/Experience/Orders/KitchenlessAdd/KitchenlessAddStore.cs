using System.Data;
using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.Experience.Orders.OrderStockConsumption;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using Npgsql;

namespace ALKAROS.Host.Experience.Orders.KitchenlessAdd;

/// <summary>
/// Adds a line the guest already had to an open check without a kitchen round: the line is stored Active with who
/// added it and why, its stock is consumed, no kitchen ticket is written, and the audit record commits with it.
/// </summary>
public sealed class KitchenlessAddStore(
    IOrderRepository orders,
    OrderStockConsumptionService consumption,
    NpgsqlDataSource dataSource,
    IAuditEventStore auditEvents)
{
    public static readonly IReadOnlySet<string> ReasonCodes =
        new HashSet<string>(StringComparer.Ordinal) { "waiter-forgot", "customer-asked", "other" };

    public async Task<KitchenlessAddResultV1> AddAsync(
        Guid orderId, KitchenlessAddRequestV1 request, Guid actorId, string correlationId, CancellationToken cancellationToken)
    {
        var reasonCode = request.ReasonCode?.Trim();
        if (reasonCode is null || !ReasonCodes.Contains(reasonCode))
            throw new KitchenlessAddReasonException();
        if (request.Quantity <= 0 || request.Quantity > 999)
            throw new KitchenlessAddQuantityException();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await using (var lockOrder = new NpgsqlCommand("SELECT 1 FROM orders.orders WHERE order_id = @order_id FOR UPDATE;", connection, transaction))
        {
            lockOrder.Parameters.AddWithValue("order_id", orderId);
            if (await lockOrder.ExecuteScalarAsync(cancellationToken) is null)
                throw new OrderNotFoundException(orderId);
        }

        var order = await orders.GetByIdAsync(orderId, cancellationToken) ?? throw new OrderNotFoundException(orderId);
        if (!order.IsOpenCheck || await HasBillAsync(connection, transaction, orderId, cancellationToken))
            throw new KitchenlessAddNotAllowedException(orderId, order.Status.ToString());

        var (sku, name, price, taxRate, allowsHalf) = await ReadProductAsync(connection, transaction, request.ProductId, cancellationToken);
        OrderQuantityRule.Require(request.Quantity, allowsHalf, request.ProductId);

        var now = DateTimeOffset.UtcNow;
        var item = new OrderItem(Guid.NewGuid(), orderId, request.ProductId, name, request.Quantity, price, taxRate, skuSnapshot: sku)
            .WithoutKitchen(actorId, reasonCode);
        var updated = order.AddWithoutKitchen(item, actorId, now);
        var rowVersion = await orders.SaveAsync(updated, request.ExpectedRowVersion, connection, transaction, cancellationToken);

        await consumption.ConsumeItemsAsync(
            updated, updated.Items.Where(i => i.Id == item.Id).ToList(), "added without kitchen", actorId, connection, transaction, cancellationToken);

        await auditEvents.AppendAsync(
            new AuditEvent(
                id: Guid.NewGuid(),
                eventName: "order.item-added-without-kitchen",
                aggregateType: "Order",
                aggregateId: orderId,
                actorType: "User",
                correlationId: correlationId,
                actorId: actorId,
                reason: reasonCode,
                beforeStateJson: JsonSerializer.Serialize(new { status = order.Status.ToString() }),
                afterStateJson: JsonSerializer.Serialize(new { itemId = item.Id, productId = request.ProductId, quantity = request.Quantity, reasonCode })),
            connection, transaction, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new KitchenlessAddResultV1(orderId, item.Id, rowVersion, reasonCode);
    }

    internal static async Task<bool> HasBillAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid orderId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            $"SELECT EXISTS (SELECT 1 FROM billing.bills b WHERE {BillOrderLink.Matches("b", "@order_id")} AND b.status <> 'Cancelled');",
            connection, transaction);
        command.Parameters.AddWithValue("order_id", orderId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<(string Sku, string Name, decimal Price, decimal TaxRate, bool AllowsHalf)> ReadProductAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid productId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT p.sku, p.name, p.current_price, COALESCE(t.vat_rate, 0), p.allows_half_portion
            FROM catalog.products p
            LEFT JOIN catalog.tax_profiles t ON t.tax_profile_id = p.tax_profile_id AND t.active
            WHERE p.product_id = @product_id AND p.active AND p.current_price IS NOT NULL;
            """, connection, transaction);
        command.Parameters.AddWithValue("product_id", productId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new KitchenlessAddProductException(productId);
        return (reader.GetString(0), reader.GetString(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetBoolean(4));
    }
}
