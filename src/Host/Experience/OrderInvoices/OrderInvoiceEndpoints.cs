using ALKAROS.Host.Composition.Errors;
using ALKAROS.Host.Experience.Reconciliation;
using ALKAROS.Invoicing.Generation.OrderInvoices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;

namespace ALKAROS.Host.Experience.OrderInvoices;

public sealed record OrderInvoiceRowV1(
    Guid InvoiceId, Guid OrderId, string OrderNumber, string Provider, DateOnly IssueDate, DateOnly ServiceDate,
    string Status, decimal NetAmount, decimal TaxAmount, decimal GrossAmount);

/// <summary>A delivered platform order that has no invoice draft yet; <see cref="DaysLeft"/> counts down the 7-day invoicing window.</summary>
public sealed record MissingOrderInvoiceV1(
    Guid OrderId, string OrderNumber, string Provider, decimal Total, DateTimeOffset DeliveredAt, int DaysLeft);

public sealed record OrderInvoiceListV1(
    DateOnly From, DateOnly To, IReadOnlyList<OrderInvoiceRowV1> Invoices, IReadOnlyList<MissingOrderInvoiceV1> Missing);

/// <summary>
/// The online order invoice drafts for managers: the drafts by service date and the delivered orders still waiting
/// for one. Read only, same gate as the channel report (manager session plus reports.view).
/// </summary>
public static class OrderInvoiceEndpoints
{
    public const int MaxDays = 31;

    public static RouteGroupBuilder MapOrderInvoiceApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ApiErrorHandling.EnsureFor(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/order-invoices");
        group.AddEndpointFilter<ReconciliationCaseEndpointFilter>();

        group.MapGet("/", async (DateOnly from, DateOnly to, NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
        {
            if (to < from || to.DayNumber - from.DayNumber >= MaxDays)
                throw new ArgumentException("The range must be between one and 31 days.");
            return Results.Ok(new OrderInvoiceListV1(
                from, to,
                await ReadInvoicesAsync(dataSource, from, to, cancellationToken),
                await ReadMissingAsync(dataSource, cancellationToken)));
        });

        group.MapGet("/by-order/{orderId:guid}", async (Guid orderId, IOrderInvoiceDraftService drafts, CancellationToken cancellationToken) =>
            await drafts.GetByOrderAsync(orderId, cancellationToken) is { } draft
                ? Results.Ok(draft)
                : Results.Json(new { error = new { code = "NOT_FOUND", message = "Bu sipariş için fatura taslağı yok." } }, statusCode: StatusCodes.Status404NotFound));

        return group;
    }

    private static async Task<IReadOnlyList<OrderInvoiceRowV1>> ReadInvoicesAsync(
        NpgsqlDataSource dataSource, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT invoice_id, order_id, order_number, provider, issue_date, service_date, status,
                   line_extension_amount, tax_total, payable_amount
            FROM invoicing.order_invoices
            WHERE service_date BETWEEN $1 AND $2
            ORDER BY service_date DESC, created_at DESC, invoice_id
            LIMIT 1000;
            """);
        command.Parameters.AddWithValue(from);
        command.Parameters.AddWithValue(to);
        var rows = new List<OrderInvoiceRowV1>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new OrderInvoiceRowV1(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
                reader.GetFieldValue<DateOnly>(4), reader.GetFieldValue<DateOnly>(5), reader.GetString(6),
                reader.GetDecimal(7), reader.GetDecimal(8), reader.GetDecimal(9)));
        return rows;
    }

    private static async Task<IReadOnlyList<MissingOrderInvoiceV1>> ReadMissingAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT o.order_id, o.order_number, l.provider, o.total, COALESCE(o.closed_at, o.updated_at) AS delivered_at,
                   GREATEST(0, 7 - floor(extract(epoch FROM now() - COALESCE(o.closed_at, o.updated_at)) / 86400))::int AS days_left
            FROM orders.orders o
            JOIN online_ordering.online_orders l ON l.order_id = o.order_id
            WHERE o.source = 'Online'
              AND o.status IN ('Served', 'Completed')
              AND COALESCE(o.closed_at, o.updated_at) > now() - interval '7 days'
              AND NOT EXISTS (SELECT 1 FROM invoicing.order_invoices i WHERE i.order_id = o.order_id)
            ORDER BY delivered_at, o.order_id
            LIMIT 200;
            """);
        var rows = new List<MissingOrderInvoiceV1>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new MissingOrderInvoiceV1(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3),
                reader.GetFieldValue<DateTimeOffset>(4), reader.GetInt32(5)));
        return rows;
    }
}
