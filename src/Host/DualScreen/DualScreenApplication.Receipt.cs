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
    decimal LineDiscount = 0m);

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
                    return Results.Json(
                        new BillingSplitErrorEnvelope(new BillingSplitError(
                            "NOT_FOUND", "İstenen hesap bulunamadı.", StatusCodes.Status404NotFound, context.TraceIdentifier)),
                        statusCode: StatusCodes.Status404NotFound);

                var adjustments = await adjustmentRepository.GetByBillIdAsync(billId, cancellationToken);
                var summary = AdjustmentCalculator.Calculate(bill, adjustments);
                var payments = await paymentRepository.GetByBillIdAsync(billId, cancellationToken);
                var methods = await PaymentMethodsAsync(dataSource, payments.Select(p => p.Id).ToArray(), cancellationToken);
                var refunded = await RefundedQuantitiesAsync(dataSource, billId, cancellationToken);
                var paidBack = await RefundedAmountsAsync(dataSource, payments.Select(p => p.Id).ToArray(), cancellationToken);

                return Results.Ok(new BillReceiptV1(
                    bill.Id,
                    bill.BillNumber,
                    bill.Status.ToString(),
                    bill.CurrencyCode,
                    bill.OpenedAt,
                    bill.ClosedAt,
                    bill.Items
                        .Select(i => new BillReceiptItemV1(
                            i.ProductNameSnapshot, i.Quantity, i.UnitPrice, i.GrossAmount, i.Id, refunded.GetValueOrDefault(i.Id),
                            i.DiscountAmount))
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
                            methods.GetValueOrDefault(p.Id, "Manual"),
                            p.Status.ToString(),
                            p.ApprovedAt,
                            paidBack.GetValueOrDefault(p.Id)))
                        .ToList(),
                    TaxBreakdown.Build(bill, summary)
                        .Select(line => new BillReceiptTaxLineV1(line.RatePercent, line.Net, line.Tax, line.Gross))
                        .ToList()));
            })
            .WithTags("BillReceipt")
            .RequireRateLimiting("terminal-read");
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

    private static async Task<Dictionary<Guid, string>> PaymentMethodsAsync(
        NpgsqlDataSource dataSource, Guid[] paymentIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, string>();
        if (paymentIds.Length == 0)
            return result;

        await using var command = dataSource.CreateCommand(
            """
            SELECT p.id,
                   CASE
                       WHEN EXISTS (SELECT 1 FROM customer_account.account_transactions a
                                    WHERE a.source_reference_type = 'Payment' AND a.source_reference_id = p.id
                                      AND a.transaction_type = 'Charge') THEN 'Account'
                       WHEN EXISTS (SELECT 1 FROM cash.cash_transactions c
                                    WHERE c.related_payment_id = p.id AND c.type = 'Sale') THEN 'Cash'
                       WHEN EXISTS (SELECT 1 FROM payments.card_settlement_attempts s
                                    WHERE s.payment_id = p.id AND s.outcome = 'Approved') THEN 'Card'
                       ELSE 'Manual' END
            FROM unnest(@ids) AS p(id);
            """);
        command.Parameters.Add("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid).Value = paymentIds;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result[reader.GetGuid(0)] = reader.GetString(1);
        return result;
    }
}
