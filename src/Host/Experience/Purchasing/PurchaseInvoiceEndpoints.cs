using ALKAROS.Purchasing.PurchaseInvoices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ALKAROS.Host.Experience.Purchasing;

// Both commands are idempotent by nature (the ETTN is unique; a mapping replaces the previous one), so the optional key is accepted for
// clients that send one but changes nothing.
public sealed record ImportPurchaseInvoiceV1(string Xml, string? IdempotencyKey = null);

public sealed record MapPurchaseInvoiceLineV1(Guid StockItemId, decimal ConversionFactor, string? IdempotencyKey = null);

public sealed record ApprovePurchaseInvoiceV1(Guid LocationId, string? IdempotencyKey = null);

public sealed record RejectPurchaseInvoiceV1(string? IdempotencyKey = null);

public static class PurchaseInvoiceEndpoints
{
    public static RouteGroupBuilder MapPurchaseInvoices(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/purchase-invoices/import", async (
            ImportPurchaseInvoiceV1 request,
            HttpContext context,
            IPurchaseInvoiceService service,
            CancellationToken cancellationToken) =>
        {
            var actorName = PurchasingManagerEndpointFilter.RequireActorDisplayName(context);
            var invoice = await service.ImportAsync(request.Xml, actorName, PurchaseInvoiceSources.XmlUpload, cancellationToken);
            return Results.Created($"/api/v1/management/purchasing/purchase-invoices/{invoice.InvoiceId:D}", invoice);
        });

        group.MapGet("/purchase-invoices", async (
            string? status,
            IPurchaseInvoiceService service,
            CancellationToken cancellationToken) => Results.Ok(await service.ListAsync(status, cancellationToken)));

        group.MapGet("/purchase-invoices/{invoiceId:guid}", async (
            Guid invoiceId,
            IPurchaseInvoiceService service,
            CancellationToken cancellationToken) => Results.Ok(await service.GetAsync(invoiceId, cancellationToken)));

        group.MapPut("/purchase-invoices/{invoiceId:guid}/lines/{lineId:guid}/mapping", async (
            Guid invoiceId,
            Guid lineId,
            MapPurchaseInvoiceLineV1 request,
            IPurchaseInvoiceService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.MapLineAsync(invoiceId, lineId, request.StockItemId, request.ConversionFactor, cancellationToken)));

        group.MapPost("/purchase-invoices/{invoiceId:guid}/approve", async (
            Guid invoiceId,
            ApprovePurchaseInvoiceV1 request,
            HttpContext context,
            IPurchaseInvoiceApprovalService service,
            CancellationToken cancellationToken) =>
        {
            var actorName = PurchasingManagerEndpointFilter.RequireActorDisplayName(context);
            var receiptId = await service.ApproveAsync(invoiceId, request.LocationId, actorName, cancellationToken);
            return Results.Ok(new { receiptId });
        });

        group.MapPost("/purchase-invoices/{invoiceId:guid}/reject", async (
            Guid invoiceId,
            RejectPurchaseInvoiceV1 request,
            IPurchaseInvoiceApprovalService service,
            CancellationToken cancellationToken) =>
        {
            await service.RejectAsync(invoiceId, cancellationToken);
            return Results.NoContent();
        });

        return group;
    }
}
