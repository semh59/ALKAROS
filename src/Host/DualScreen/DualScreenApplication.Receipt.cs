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
    decimal CompletedRefundedQuantity, decimal LineDiscount = 0m, string? AddedWithoutKitchenReason = null);

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
    IReadOnlyList<BillReceiptTaxLineV1>? TaxLines = null,
    DateTimeOffset? OriginalPrintedAt = null,
    bool IsCopy = false,
    string? TableNumber = null);

public sealed record BillReceiptOriginalResetRequestV1(string? Reason, Guid IdempotencyKey);

public sealed record BillReceiptOriginalResetResultV1(Guid BillId, DateTimeOffset PreviousPrintedAt);

public static partial class DualScreenApplication
{
    private const int MaxReceiptResetReasonLength = 500;

    /// <summary>How often the right to print a bill's original can be given back; counted from the recorded resets.</summary>
    private const int MaxReceiptResets = 2;

    /// <summary>
    /// The bill summary the cashier page prints as a non-fiscal information receipt: items, adjustments, totals and the
    /// payments with the method they were taken by. Reading it writes nothing, but it carries the print state of the bill
    /// (<see cref="BillReceiptV1.OriginalPrintedAt"/>, <see cref="BillReceiptV1.IsCopy"/>) so whatever sheet is drawn from
    /// it is marked a copy once the original is out. Printing goes through the print endpoint, which settles that state.
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
                IBillReceiptPrintRepository receiptPrints,
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

                var originalPrintedAt = await receiptPrints.FindOriginalPrintedAtAsync(billId, cancellationToken);
                return Results.Ok(await BuildReceiptAsync(
                    bill, adjustmentRepository, paymentRepository, dataSource, originalPrintedAt, originalPrintedAt is not null, cancellationToken));
            })
            .WithTags("BillReceipt")
            .AddEndpointFilter<BillTerminalAccessFilter>()
            .RequireRateLimiting("terminal-read");

        // The first print of a settled bill's receipt is the original; every later one is a copy. Which it is gets decided and
        // recorded in one transaction before the data is handed out, so neither can leave the till unrecorded and two
        // terminals printing at once cannot both get the original.
        app.MapPost("/api/v1/terminals/{terminalId:guid}/billing/bills/{billId:guid}/receipt/print", async (
                Guid terminalId,
                Guid billId,
                IBillRepository billRepository,
                IBillAdjustmentRepository adjustmentRepository,
                IPaymentRepository paymentRepository,
                IBillReceiptPrintRepository receiptPrints,
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
                        context, StatusCodes.Status409Conflict, "BILL_NOT_PAID", "Yalnız ödenmiş hesabın fişi yazdırılabilir.");

                // The sheet is read before the original is claimed, so a failed read never uses the original up.
                var receipt = await BuildReceiptAsync(
                    bill, adjustmentRepository, paymentRepository, dataSource, null, false, cancellationToken);
                var claim = await RecordPrintAsync(
                    dataSource, receiptPrints, auditEvents, bill, terminalId, principal.UserId, context.TraceIdentifier, cancellationToken);
                return claim.Outcome switch
                {
                    ReceiptPrintOutcome.Original or ReceiptPrintOutcome.Copy => Results.Ok(
                        receipt with { OriginalPrintedAt = claim.OriginalPrintedAt, IsCopy = claim.Outcome == ReceiptPrintOutcome.Copy }),
                    ReceiptPrintOutcome.NotFound => ReceiptError(context, StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen hesap bulunamadı."),
                    _ => ReceiptError(
                        context, StatusCodes.Status409Conflict, "BILL_NOT_PAID", "Yalnız ödenmiş hesabın fişi yazdırılabilir."),
                };
            })
            .WithTags("BillReceipt")
            .WithMetadata(new BillRouteDoesNotClaim())
            .AddEndpointFilter<BillTerminalAccessFilter>()
            .RequireRateLimiting("terminal-write");

        // A print that was cancelled or whose answer got lost still used up the original; a manager gives it back with a
        // reason, which is kept in the audit trail together with who had printed it and when.
        app.MapPost("/api/v1/terminals/{terminalId:guid}/billing/bills/{billId:guid}/receipt/original-reset", async (
                Guid terminalId,
                Guid billId,
                BillReceiptOriginalResetRequestV1 request,
                IBillRepository billRepository,
                IBillReceiptPrintRepository receiptPrints,
                IAuditEventStore auditEvents,
                NpgsqlDataSource dataSource,
                DualScreenStore store,
                IAuthorizationService authorization,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var principal = await RequireCashierPermissionAsync(
                    context, terminalId, store, authorization, ApplicationPermissions.PaymentsRefundApprove, cancellationToken);

                var reason = request?.Reason?.Trim();
                if ((request?.IdempotencyKey ?? Guid.Empty) == Guid.Empty)
                    return ReceiptError(context, StatusCodes.Status400BadRequest, "IDEMPOTENCY_KEY_REQUIRED", "İstek bir tekrar anahtarı taşımalı.");
                if (string.IsNullOrEmpty(reason) || reason.Length > MaxReceiptResetReasonLength)
                    return ReceiptError(
                        context, StatusCodes.Status400BadRequest, "REASON_REQUIRED", "Sıfırlama gerekçesi 1 ile 500 karakter arasında olmalı.");

                var bill = await billRepository.GetByIdAsync(billId, cancellationToken);
                if (bill is null)
                    return ReceiptError(context, StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen hesap bulunamadı.");

                await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                var replayed = await FindResetReplayAsync(billId, request!.IdempotencyKey, connection, transaction, cancellationToken);
                if (replayed is not null)
                    return Results.Ok(new BillReceiptOriginalResetResultV1(billId, replayed.Value));
                var claim = await receiptPrints.ResetOriginalAsync(billId, connection, transaction, cancellationToken);
                if (claim.Outcome == ReceiptResetOutcome.NotFound)
                    return ReceiptError(context, StatusCodes.Status404NotFound, "NOT_FOUND", "İstenen hesap bulunamadı.");
                if (claim.Outcome == ReceiptResetOutcome.NothingToReset)
                    return ReceiptError(
                        context, StatusCodes.Status409Conflict, "RECEIPT_NOT_PRINTED", "Bu hesabın fişi henüz basılmadı; sıfırlanacak orijinal yok.");

                // Returning early leaves the transaction uncommitted, so the original stays used up.
                if (claim.PreviousPrintedBy == principal.UserId)
                    return ReceiptError(
                        context, StatusCodes.Status403Forbidden, "RECEIPT_RESET_SELF", "Orijinal fişi siz bastınız; hakkı başka bir yönetici sıfırlamalı.");
                await using (var resets = new NpgsqlCommand(
                    "SELECT count(*) FROM audit.audit_events WHERE aggregate_type = 'Bill' AND aggregate_id = @id AND event_name = 'bill.receipt.original.reset';",
                    connection, transaction))
                {
                    resets.Parameters.AddWithValue("id", billId);
                    if ((long)(await resets.ExecuteScalarAsync(cancellationToken))! >= MaxReceiptResets)
                        return ReceiptError(
                            context, StatusCodes.Status409Conflict, "RECEIPT_RESET_LIMIT",
                            $"Bu hesabın orijinal fiş hakkı en fazla {MaxReceiptResets} kez sıfırlanabilir.");
                }

                await auditEvents.AppendAsync(
                    new AuditEvent(
                        Guid.NewGuid(), "bill.receipt.original.reset", "Bill", billId, "User", context.TraceIdentifier, principal.UserId,
                        reason: reason,
                        afterStateJson: JsonSerializer.Serialize(new
                        {
                            bill.BillNumber, terminalId, previousPrintedAt = claim.PreviousPrintedAt, previousPrintedBy = claim.PreviousPrintedBy, idempotencyKey = request.IdempotencyKey,
                        })),
                    connection, transaction, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Results.Ok(new BillReceiptOriginalResetResultV1(billId, claim.PreviousPrintedAt!.Value));
            })
            .WithTags("BillReceipt")
            .WithMetadata(new BillRouteDoesNotClaim())
            .AddEndpointFilter<BillTerminalAccessFilter>()
            .RequireRateLimiting("terminal-write");
    }

    private static async Task<DateTimeOffset?> FindResetReplayAsync(
        Guid billId, Guid idempotencyKey, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT after_state_json->>'previousPrintedAt' FROM audit.audit_events WHERE aggregate_type = 'Bill' AND aggregate_id = @id " +
            "AND event_name = 'bill.receipt.original.reset' AND after_state_json->>'idempotencyKey' = @key LIMIT 1;",
            connection, transaction);
        command.Parameters.AddWithValue("id", billId);
        command.Parameters.AddWithValue("key", idempotencyKey.ToString());
        return await command.ExecuteScalarAsync(cancellationToken) is string text
            ? DateTimeOffset.Parse(text, System.Globalization.CultureInfo.InvariantCulture)
            : null;
    }

    private static IResult ReceiptError(HttpContext context, int status, string code, string message)
        => Results.Json(
            new BillingSplitErrorEnvelope(new BillingSplitError(code, message, status, context.TraceIdentifier)),
            statusCode: status);

    private static async Task<ReceiptPrintClaim> RecordPrintAsync(
        NpgsqlDataSource dataSource,
        IBillReceiptPrintRepository receiptPrints,
        IAuditEventStore auditEvents,
        Bill bill,
        Guid terminalId,
        Guid userId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // The paid check, the claim and the audit event share one transaction under the bill's row lock: a refused request
        // leaves nothing behind, and an audit failure takes the claim with it.
        var claim = await receiptPrints.ClaimOriginalAsync(bill.Id, userId, connection, transaction, cancellationToken);
        if (claim.Outcome is ReceiptPrintOutcome.NotFound or ReceiptPrintOutcome.NotPaid)
            return claim;

        await auditEvents.AppendAsync(
            new AuditEvent(
                Guid.NewGuid(), claim.Outcome == ReceiptPrintOutcome.Original ? "bill.receipt.printed" : "bill.receipt.reprinted",
                "Bill", bill.Id, "User", correlationId, userId,
                afterStateJson: JsonSerializer.Serialize(new { bill.BillNumber, terminalId })),
            connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claim;
    }

    private static async Task<BillReceiptV1> BuildReceiptAsync(
        Bill bill,
        IBillAdjustmentRepository adjustmentRepository,
        IPaymentRepository paymentRepository,
        NpgsqlDataSource dataSource,
        DateTimeOffset? originalPrintedAt,
        bool isCopy,
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
        var withoutKitchen = await BillOrderNotes.WithoutKitchenReasonsAsync(dataSource, bill.Items.Select(i => i.OrderItemId), cancellationToken);

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
                    completedRefunded.GetValueOrDefault(i.Id), i.DiscountAmount, withoutKitchen.GetValueOrDefault(i.OrderItemId)))
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
                .ToList(),
            originalPrintedAt,
            isCopy,
            await BillOrderNotes.TableNumberAsync(dataSource, bill.TableId, cancellationToken));
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
