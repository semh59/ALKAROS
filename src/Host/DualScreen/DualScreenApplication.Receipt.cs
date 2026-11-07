using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.Experience.Billing;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Payments.PaymentAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Npgsql;
using NpgsqlTypes;

namespace ALKAROS.Host.DualScreen;

public sealed record BillReceiptItemV1(
    string Name, decimal Quantity, decimal UnitPrice, decimal Amount, Guid BillItemId, decimal RefundedQuantity,
    decimal CompletedRefundedQuantity, decimal LineDiscount = 0m);

public sealed record BillReceiptTaxLineV1(decimal RatePercent, decimal Net, decimal Tax, decimal Gross);

public sealed record BillReceiptAdjustmentV1(string Type, decimal Amount, bool IsDeduction);

public sealed record BillReceiptPaymentV1(
    Guid PaymentId, decimal Amount, string Method, string Status, DateTimeOffset? At, decimal RefundedAmount);

public sealed record BillReceiptV1(
    Guid BillId,
    string BillNumber,
    string Status,
    string CurrencyCode,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<BillReceiptItemV1> Items,
    IReadOnlyList<BillReceiptAdjustmentV1> Adjustments,
    decimal Subtotal,
    decimal PayableAmount,
    decimal TaxTotal,
    IReadOnlyList<BillReceiptPaymentV1> Payments,
    IReadOnlyList<BillReceiptTaxLineV1>? TaxLines = null);

public static partial class DualScreenApplication
{
    /// <summary>
    /// Read-only bill summary the cashier page prints as a non-fiscal information receipt: items, adjustments,
    /// totals and the payments with the method they were taken by. Writes nothing.
    /// </summary>
    public static void MapBillReceiptApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/terminals/{terminalId:guid}/billing/bills/{billId:guid}/receipt", async (
                Guid terminalId,
                Guid billId,
                IBillRepository billRepository,
                IBillAdjustmentRepository adjustmentRepository,
                IPaymentRepository paymentRepository,
                NpgsqlDataSource dataSource,
                DualScreenStore store,
                IAuthorizationService authorization,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                await RequireCashierPermissionAsync(
                    context, terminalId, store, authorization, ApplicationPermissions.PaymentsTake, cancellationToken);

                var bill = await billRepository.GetByIdAsync(billId, cancellationToken);
                if (bill is null)
                    return ReceiptError(context, StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen hesap bulunamadı.");

                return Results.Ok(await BuildReceiptAsync(
                    bill, adjustmentRepository, paymentRepository, dataSource, cancellationToken));
            })
            .WithTags("BillReceipt")
            .AddEndpointFilter<BillTerminalAccessFilter>()
            .RequireRateLimiting("terminal-read");

        // Every reprint of a settled bill's receipt is recorded before the data is handed out, so a copy cannot leave
        // the till unrecorded; the page marks the sheet KOPYA.
        app.MapPost("/api/v1/terminals/{terminalId:guid}/billing/bills/{billId:guid}/receipt/reprint", async (
                Guid terminalId,
                Guid billId,
                IBillRepository billRepository,
                IBillAdjustmentRepository adjustmentRepository,
                IPaymentRepository paymentRepository,
                IAuditEventStore auditEvents,
                NpgsqlDataSource dataSource,
                DualScreenStore store,
                IAuthorizationService authorization,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var principal = await RequireCashierPermissionAsync(
                    context, terminalId, store, authorization, ApplicationPermissions.PaymentsTake, cancellationToken);

                var bill = await billRepository.GetByIdAsync(billId, cancellationToken);
                if (bill is null)
                    return ReceiptError(context, StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen hesap bulunamadı.");
                if (bill.Status != BillState.Paid)
                    return ReceiptError(
                        context, StatusCodes.Status409Conflict, "BILL_NOT_PAID", "Yalnız ödenmiş hesabın fişi yeniden yazdırılabilir.");

                var receipt = await BuildReceiptAsync(bill, adjustmentRepository, paymentRepository, dataSource, cancellationToken);
                await auditEvents.AppendAsync(
                    new AuditEvent(
                        Guid.NewGuid(), "bill.receipt.reprinted", "Bill", billId, "User", context.TraceIdentifier,
                        principal.UserId,
                        afterStateJson: JsonSerializer.Serialize(new { bill.BillNumber, terminalId })),
                    cancellationToken);
                return Results.Ok(receipt);
            })
            .WithTags("BillReceipt")
            .WithMetadata(new BillRouteDoesNotClaim())
            .AddEndpointFilter<BillTerminalAccessFilter>()
            .RequireRateLimiting("terminal-write");
    }

    private static IResult ReceiptError(HttpContext context, int status, string code, string message)
        => Results.Json(
            new BillingSplitErrorEnvelope(new BillingSplitError(code, message, status, context.TraceIdentifier)),
            statusCode: status);

    private static async Task<BillReceiptV1> BuildReceiptAsync(
        Bill bill,
        IBillAdjustmentRepository adjustmentRepository,
        IPaymentRepository paymentRepository,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var billId = bill.Id;
        var adjustments = await adjustmentRepository.GetByBillIdAsync(billId, cancellationToken);
        var summary = AdjustmentCalculator.Calculate(bill, adjustments);
        var payments = await paymentRepository.GetByBillIdAsync(billId, cancellationToken);
        var methods = await PaymentMethodLookup.ForPaymentsAsync(dataSource, payments.Select(p => p.Id).ToArray(), cancellationToken);
        var refunded = await RefundedQuantitiesAsync(dataSource, billId, cancellationToken);
        var completedRefunded = await CompletedRefundedQuantitiesAsync(dataSource, billId, cancellationToken);
        var paidBack = await RefundedAmountsAsync(dataSource, payments.Select(p => p.Id).ToArray(), cancellationToken);

        return new BillReceiptV1(
            bill.Id,
            bill.BillNumber,
            bill.Status.ToString(),
            bill.CurrencyCode,
            bill.OpenedAt,
            bill.ClosedAt,
            bill.Items
                .Select(i => new BillReceiptItemV1(
                    i.ProductNameSnapshot, i.Quantity, i.UnitPrice, i.GrossAmount, i.Id, refunded.GetValueOrDefault(i.Id),
                    completedRefunded.GetValueOrDefault(i.Id), i.DiscountAmount))
                .ToList(),
            adjustments
                .Select(a => new BillReceiptAdjustmentV1(a.AdjustmentType.ToString(), a.GrossAmount, a.IsDeduction))
                .ToList(),
            // The printed lines are after any line discount or complimentary, so the subtotal is their sum.
            bill.PayableAmount,
            summary.AdjustedPayableAmount,
            summary.AdjustedTaxTotal,
            payments
                .Where(p => p.ApprovedAmount is > 0m)
                .OrderBy(p => p.ApprovedAt ?? p.InitiatedAt)
                .Select(p => new BillReceiptPaymentV1(
                    p.Id,
                    p.ApprovedAmount!.Value,
                    methods[p.Id],
                    p.Status.ToString(),
                    p.ApprovedAt,
                    paidBack.GetValueOrDefault(p.Id)))
                .ToList(),
            TaxBreakdown.Build(bill, summary)
                .Select(line => new BillReceiptTaxLineV1(line.RatePercent, line.Net, line.Tax, line.Gross))
                .ToList());
    }

    private static async Task<Dictionary<Guid, decimal>> RefundedAmountsAsync(
        NpgsqlDataSource dataSource, Guid[] paymentIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, decimal>();
        if (paymentIds.Length == 0)
            return result;
        await using var command = dataSource.CreateCommand(
            "SELECT payment_id, SUM(amount) FROM payments.payment_reversals WHERE payment_id = ANY(@ids) GROUP BY payment_id;");
        command.Parameters.Add("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = paymentIds;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result[reader.GetGuid(0)] = reader.GetDecimal(1);
        return result;
    }

    private static async Task<Dictionary<Guid, decimal>> RefundedQuantitiesAsync(
        NpgsqlDataSource dataSource, Guid billId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT i.bill_item_id, SUM(i.quantity)
            FROM payments.refund_request_items i
            JOIN payments.refund_requests r ON r.refund_request_id = i.refund_request_id
            WHERE r.bill_id = @bill AND r.status IN ('Requested', 'AwaitingManualRefund', 'Completed')
            GROUP BY i.bill_item_id;
            """);
        command.Parameters.Add("bill", NpgsqlDbType.Uuid).Value = billId;
        var result = new Dictionary<Guid, decimal>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result[reader.GetGuid(0)] = reader.GetDecimal(1);
        return result;
    }

    private static async Task<Dictionary<Guid, decimal>> CompletedRefundedQuantitiesAsync(
        NpgsqlDataSource dataSource, Guid billId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT i.bill_item_id, SUM(i.quantity)
            FROM payments.refund_request_items i
            JOIN payments.refund_requests r ON r.refund_request_id = i.refund_request_id
            WHERE r.bill_id = @bill AND r.status = 'Completed'
            GROUP BY i.bill_item_id;
            """);
        command.Parameters.Add("bill", NpgsqlDbType.Uuid).Value = billId;
        var result = new Dictionary<Guid, decimal>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result[reader.GetGuid(0)] = reader.GetDecimal(1);
        return result;
    }
}
