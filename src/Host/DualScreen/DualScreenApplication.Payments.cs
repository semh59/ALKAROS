using ALKAROS.Billing.BillFoundation;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.EftTender;
using ALKAROS.Payments.PaymentAggregate;
using ALKAROS.Payments.TenderRouting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;

namespace ALKAROS.Host.DualScreen;

public static partial class DualScreenApplication
{
    /// <summary>
    /// V13-PUI-001: the generic tender-routing HTTP surface for the split
    /// payment UI — BankCard and Eft only. Cash keeps its own dedicated
    /// <c>POST .../cash-sessions/{cashSessionId}/cash-tender</c> endpoint
    /// (V13-CSH-004, unmodified): it needs a <c>CashSessionId</c> and mints
    /// its own real Payment id, neither of which the generic
    /// <see cref="TenderRouter"/> envelope carries faithfully (see
    /// <c>TenderRequest</c>'s own doc comment) — routing Cash through here
    /// too would only add an indirection with no real benefit. MealCard is
    /// never accepted here either: it stays genuinely unregistered
    /// (V13-PAY-003), so the router's own <see cref="TenderMethodNotRegistered"/>
    /// already gives it the correct typed-unavailable response with zero
    /// special-casing.
    /// </summary>
    public static RouteGroupBuilder MapPaymentTenderApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/terminals/{terminalId:guid}/billing/bills/{billId:guid}/tenders")
            .WithTags("PaymentTender")
            .RequireRateLimiting("terminal-write");

        group.MapGet("/", async (
            Guid terminalId,
            Guid billId,
            IBillRepository billRepository,
            IPaymentAllocationRepository allocationRepository,
            IPaymentRepository paymentRepository,
            DualScreenStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierAsync(context, terminalId, store, cancellationToken);

            var bill = await billRepository.GetByIdAsync(billId, cancellationToken);
            if (bill is null)
                return Results.NotFound();

            var allocations = await allocationRepository.GetByBillIdAsync(billId, cancellationToken);
            var allocatedTotal = allocations.Sum(a => a.Amount);
            var remaining = bill.PayableAmount - allocatedTotal;

            // No PaymentAllocation is ever created for a
            // TenderRequiresReconciliation outcome today (the BankCard
            // placeholder handler persists nothing — there is no real
            // terminal result to record), so an Unknown/pending BankCard
            // attempt cannot be detected by reading allocations alone. The
            // client-side lock (see split-payment.js) is therefore the real
            // enforcement point for "don't let the cashier retry a pending
            // BankCard line into a duplicate charge" until V13-HUG-001
            // lands and a real Payment row can carry that state.
            var lines = new List<PaymentAllocationLineV1>(allocations.Count);
            foreach (var allocation in allocations)
            {
                var payment = await paymentRepository.GetByIdAsync(allocation.PaymentId, cancellationToken);
                lines.Add(new PaymentAllocationLineV1(
                    allocation.Id,
                    allocation.PaymentId,
                    allocation.Amount,
                    payment?.Status.ToString() ?? "Unknown"));
            }

            return Results.Ok(new BillTenderSummaryV1(
                billId, bill.PayableAmount, allocatedTotal, remaining, lines));
        });

        group.MapPost("/", async (
            Guid terminalId,
            Guid billId,
            SubmitBillTenderRequestV1 request,
            TenderRouter router,
            DualScreenStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierAsync(context, terminalId, store, cancellationToken);

            if (!TenderMethodCatalog.TryParse(request.Method, out var method))
                return Results.Json(
                    new { error = new { code = "TENDER_METHOD_UNKNOWN", message = "Tanınmayan ödeme yöntemi." } },
                    statusCode: StatusCodes.Status400BadRequest);

            if (method == TenderMethod.Cash)
                return Results.Json(
                    new
                    {
                        error = new
                        {
                            code = "USE_CASH_TENDER_ENDPOINT",
                            message = "Nakit ödeme için kasa oturumunun kendi tahsilat adresi kullanılmalı.",
                        },
                    },
                    statusCode: StatusCodes.Status400BadRequest);

            var tenderRequest = new TenderRequest(
                PaymentId: Guid.NewGuid(),
                Method: method,
                Amount: request.Amount,
                BillId: billId,
                IdempotencyKey: request.IdempotencyKey,
                RecordedBy: principal.UserId,
                Note: string.IsNullOrWhiteSpace(request.Note) ? null : request.Note);

            TenderRoutingResult routing;
            try
            {
                routing = await router.RouteAsync(tenderRequest, cancellationToken);
            }
            catch (EftOverTenderException ex)
            {
                return Results.Json(
                    new
                    {
                        error = new
                        {
                            code = "TENDER_OVER_ALLOCATION",
                            message = $"Tutar kalan {ex.RemainingPayable:0.00} ₺'yi aşıyor.",
                        },
                    },
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (EftTenderBillNotFoundException)
            {
                return Results.NotFound();
            }

            return routing switch
            {
                TenderRoutingHandled { Result: TenderApproved approved } =>
                    Results.Ok(new SubmitBillTenderResultV1("Approved", approved.ApprovedAmount, null)),
                TenderRoutingHandled { Result: TenderDeclined declined } =>
                    Results.Ok(new SubmitBillTenderResultV1("Declined", null, declined.Reason)),
                TenderRoutingHandled { Result: TenderRequiresReconciliation pending } =>
                    Results.Ok(new SubmitBillTenderResultV1("RequiresReconciliation", null, pending.Reason)),
                TenderMethodNotRegistered notRegistered => Results.Json(
                    new { error = new { code = TenderMethodNotRegistered.Code, message = notRegistered.Message } },
                    statusCode: StatusCodes.Status409Conflict),
                TenderVersionNotEnabled notEnabled => Results.Json(
                    new { error = new { code = TenderVersionNotEnabled.Code, message = notEnabled.Message } },
                    statusCode: StatusCodes.Status400BadRequest),
                _ => Results.Json(
                    new { error = new { code = "INTERNAL_ERROR", message = "İşlem tamamlanamadı." } },
                    statusCode: StatusCodes.Status500InternalServerError),
            };
        });

        return group;
    }
}

/// <summary>V13-PUI-001: request/response DTOs for the generic (non-Cash) tender HTTP surface.</summary>
public sealed record SubmitBillTenderRequestV1(string Method, decimal Amount, string IdempotencyKey, string? Note = null);

public sealed record SubmitBillTenderResultV1(string Outcome, decimal? ApprovedAmount, string? Reason);

public sealed record PaymentAllocationLineV1(Guid AllocationId, Guid PaymentId, decimal Amount, string PaymentStatus);

public sealed record BillTenderSummaryV1(
    Guid BillId,
    decimal PayableAmount,
    decimal AllocatedTotal,
    decimal RemainingAmount,
    IReadOnlyList<PaymentAllocationLineV1> Allocations);
