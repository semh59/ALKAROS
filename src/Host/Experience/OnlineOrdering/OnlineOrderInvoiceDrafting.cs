using ALKAROS.Invoicing.Generation.OrderInvoices;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.OnlineOrdering;

/// <summary>
/// Drafts the e-Arsiv invoice of a handed-over platform order. It runs after the handover has committed and never
/// throws for an invoice problem: a missing seller profile or a failed write leaves the order without a draft, and the
/// scheduled pass drafts it while the 7-day invoicing window (from delivery) is still open.
/// </summary>
public sealed class OnlineOrderInvoiceDrafting(
    NpgsqlDataSource dataSource,
    IOrderRepository orders,
    IOrderInvoiceDraftService invoices,
    ILogger<OnlineOrderInvoiceDrafting> logger)
{
    public static readonly TimeSpan InvoicingWindow = TimeSpan.FromDays(7);
    private const int PassLimit = 50;
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    private static readonly Dictionary<string, string> WebAddresses = new(StringComparer.Ordinal)
    {
        ["yemeksepeti"] = "https://www.yemeksepeti.com",
        ["trendyol-go"] = "https://www.trendyol.com"
    };

    private static readonly Action<ILogger, Guid, string, Exception?> LogDraftFailure =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Warning,
            new EventId(5580, nameof(LogDraftFailure)),
            "The invoice draft of online order {OrderId} was not created ({Reason}); the scheduled pass will try again.");

    private static readonly Action<ILogger, Exception?> LogPassFault =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(5581, nameof(LogPassFault)),
            "Looking for online orders without an invoice draft failed; it will be retried on the next pass.");

    /// <summary>True when the order has a draft afterwards (new or already there).</summary>
    public async Task<bool> TryDraftAsync(Guid orderId, Guid? createdBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var order = await orders.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false)
                ?? throw new OrderNotFoundException(orderId);
            var link = await FindLinkAsync(orderId, cancellationToken).ConfigureAwait(false)
                ?? throw new NotAnOnlineOrderException(orderId);

            var lines = order.Items
                .Where(item => item.CountsInOrderTotals && item.GrossAmount > 0)
                .Select(item => new OrderInvoiceLineInput(
                    item.ProductNameSnapshot, item.Quantity, item.TaxRate, item.NetAmount, item.TaxAmount, item.GrossAmount))
                .ToList();
            var delivered = order.ClosedAt ?? order.UpdatedAt;
            var input = new OrderInvoiceInput(
                order.Id, link.Provider, link.ExternalOrderId, order.OrderNumber,
                DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(delivered, Istanbul).DateTime),
                WebAddresses.TryGetValue(link.Provider, out var address) ? address : throw new InvalidOperationException(
                    $"No web address is known for the platform '{link.Provider}'."),
                PaymentMethod: null, PaymentDate: null, CarrierName: null, CarrierTaxId: null, lines);

            await invoices.CreateAsync(input, createdBy, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogDraftFailure(logger, orderId, ex.GetType().Name, null);
            return false;
        }
    }

    /// <summary>Drafts the orders handed over within the invoicing window that still have none; returns how many it created.</summary>
    public async Task<int> DraftMissingAsync(CancellationToken cancellationToken = default)
    {
        List<Guid> missing = [];
        try
        {
            await using var command = dataSource.CreateCommand(
                """
                SELECT o.order_id
                FROM orders.orders o
                JOIN online_ordering.online_orders l ON l.order_id = o.order_id
                WHERE o.source = 'Online'
                  AND o.status IN ('Served', 'Completed')
                  AND COALESCE(o.closed_at, o.updated_at) > now() - $1
                  AND NOT EXISTS (SELECT 1 FROM invoicing.order_invoices i WHERE i.order_id = o.order_id)
                ORDER BY o.created_at, o.order_id
                LIMIT $2;
                """);
            command.Parameters.AddWithValue(InvoicingWindow);
            command.Parameters.AddWithValue(PassLimit);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                missing.Add(reader.GetGuid(0));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPassFault(logger, ex);
            return 0;
        }

        var created = 0;
        foreach (var orderId in missing)
            if (await TryDraftAsync(orderId, null, cancellationToken).ConfigureAwait(false))
                created++;
        return created;
    }

    private async Task<(string Provider, string ExternalOrderId)?> FindLinkAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT provider, external_order_id FROM online_ordering.online_orders WHERE order_id = $1;");
        command.Parameters.AddWithValue(orderId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? (reader.GetString(0), reader.GetString(1))
            : null;
    }
}
