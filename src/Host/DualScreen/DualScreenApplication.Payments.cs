using System.Text.Json;
using ALKAROS.Audit.EventStore;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Identity.Authorization;
using ALKAROS.Payments.ManualResolution;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.CardSettlement;
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
    /// V13-PUI-001/V1-RMD-258: the generic tender-routing HTTP surface for
    /// the split payment UI — BankCard and Eft only. Cash keeps its own
    /// dedicated <c>POST .../cash-sessions/{cashSessionId}/cash-tender</c>
    /// endpoint (V13-CSH-004, unmodified): it needs a <c>CashSessionId</c>
    /// and mints its own real Payment id, neither of which the generic
    /// <see cref="TenderRouter"/> envelope carries faithfully (see
    /// <c>TenderRequest</c>'s own doc comment) — routing Cash through here
    /// too would only add an indirection with no real benefit. MealCard is
    /// never accepted here either: it stays genuinely unregistered
    /// (V13-PAY-003), so the router's own <see cref="TenderMethodNotRegistered"/>
    /// already gives it the correct typed-unavailable response with zero
    /// special-casing.
    ///
    /// V1-RMD-258: a BankCard routing result is no longer relayed to the
    /// caller unpersisted — it is handed to
    /// <see cref="ICardSettlementOrchestrator"/>, which durably records the
    /// attempt (Approved/Declined/RequiresReconciliation) exactly as
    /// V13-PAY-004 built it. This closes two findings from the Faz 2
    /// independent audit at their shared root: (1) <c>CardSettlementOrchestrator</c>
    /// had zero real callers despite being fully built and tested; (2) an
    /// Unknown/RequiresReconciliation BankCard attempt left no trace in the
    /// database, so the "don't let the cashier retry into a duplicate
    /// charge" lock lived only in the browser's own JS memory and was lost
    /// on a page reload. The GET endpoint below now surfaces a real
    /// unresolved Payment (if any) read from the database, so the client can
    /// correctly re-derive its lock state after a reload instead of relying
    /// on in-memory state alone.
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

            var payments = await paymentRepository.GetByBillIdAsync(billId, cancellationToken);
            var paymentsById = payments.ToDictionary(p => p.Id);

            var lines = new List<PaymentAllocationLineV1>(allocations.Count);
            foreach (var allocation in allocations)
            {
                paymentsById.TryGetValue(allocation.PaymentId, out var payment);
                lines.Add(new PaymentAllocationLineV1(
                    allocation.Id,
                    allocation.PaymentId,
                    allocation.Amount,
                    payment?.Status.ToString() ?? "Unknown"));
            }

            // V1-RMD-258: a genuinely persisted Pending/Unknown/
            // ReconciliationRequired Payment (CardSettlementOrchestrator now
            // writes one for a RequiresReconciliation BankCard outcome) is
            // read here for real — the client uses this to re-derive its
            // lock state on every load, so a page reload can no longer
            // silently drop the "don't retry into a duplicate charge" lock.
            var unsettled = payments.FirstOrDefault(p =>
                p.Status is PaymentStatus.Pending or PaymentStatus.Unknown or PaymentStatus.ReconciliationRequired);
            var unsettledDto = unsettled is null
                ? null
                : new UnsettledPaymentV1(
                    unsettled.Id,
                    unsettled.Status.ToString(),
                    unsettled.History.Count > 0 ? unsettled.History[^1].Reason : null);

            return Results.Ok(new BillTenderSummaryV1(
                billId, bill.PayableAmount, allocatedTotal, remaining, lines, unsettledDto));
        });

        // V1-RMD-264: an unconfirmed card payment locks its bill (no real
        // terminal exists to settle it). A manager - never a plain cashier -
        // can declare "the card was NOT charged", which closes the payment as
        // Declined and unlocks the bill. Creates no money record.
        group.MapPost("/unsettled/{paymentId:guid}/not-charged", async (
            Guid terminalId,
            Guid billId,
            Guid paymentId,
            ResolveUnsettledPaymentRequestV1 request,
            IManualPaymentResolutionService resolution,
            IPaymentRepository paymentRepository,
            IAuditEventStore auditEvents,
            IAuthorizationService authorization,
            DualScreenStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierPermissionAsync(
                context, terminalId, store, authorization, ApplicationPermissions.ReconciliationManage, cancellationToken);

            var payment = await paymentRepository.GetByIdAsync(paymentId, cancellationToken);
            if (payment is null || payment.BillId != billId)
                return Results.NotFound();

            try
            {
                var result = await resolution.MarkNotChargedAsync(
                    paymentId, principal.UserId, request.Reason ?? string.Empty, cancellationToken);
                await auditEvents.AppendAsync(
                    new AuditEvent(
                        id: Guid.NewGuid(),
                        eventName: "payment.manual-resolution.not-charged",
                        aggregateType: "Payment",
                        aggregateId: paymentId,
                        actorType: "User",
                        correlationId: context.TraceIdentifier,
                        actorId: principal.UserId,
                        reason: request.Reason?.Trim(),
                        beforeStateJson: JsonSerializer.Serialize(new { status = result.PreviousStatus, billId }),
                        afterStateJson: JsonSerializer.Serialize(new { status = result.NewStatus, billId })),
                    cancellationToken);
                return Results.Ok(new ResolveUnsettledPaymentResultV1(paymentId, result.NewStatus));
            }
            catch (ManualResolutionReasonInvalidException)
            {
                return Results.Json(
                    new { error = new { code = "RESOLUTION_REASON_INVALID", message = "Gerekçe yazmalısınız (en fazla 500 karakter)." } },
                    statusCode: StatusCodes.Status400BadRequest);
            }
            catch (ManualResolutionNotResolvableException)
            {
                return Results.Json(
                    new { error = new { code = "PAYMENT_NOT_RESOLVABLE", message = "Bu ödeme artık elle çözülemez; sayfayı yenileyip durumu kontrol edin." } },
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (ManualResolutionPaymentNotFoundException)
            {
                return Results.NotFound();
            }
        });

        group.MapPost("/", async (
            Guid terminalId,
            Guid billId,
            SubmitBillTenderRequestV1 request,
            TenderRouter router,
            ICardSettlementOrchestrator cardSettlementOrchestrator,
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

                // V1-RMD-258: BankCard's routing result is never returned to
                // the caller unpersisted — it is durably recorded through
                // CardSettlementOrchestrator (V13-PAY-004), the same
                // orchestrator built for exactly this purpose but never
                // wired to any real caller until now. The provider
                // correlation id is honestly the request's own idempotency
                // key, not a real terminal reference — no real terminal call
                // was ever made (the BankCard handler is still
                // PendingBankCardTerminalIntegrationHandler's honest
                // placeholder); this will be replaced by the terminal's own
                // reference the day V13-HUG-001 ships a real handler.
                if (method == TenderMethod.BankCard && routing is TenderRoutingHandled { Result: var cardResult })
                {
                    var settlement = await cardSettlementOrchestrator.HandleAsync(
                        new CardSettlementRequest(
                            billId, request.Amount, request.IdempotencyKey, request.IdempotencyKey, cardResult),
                        cancellationToken);

                    return settlement.Outcome switch
                    {
                        CardSettlementOutcome.Approved =>
                            Results.Ok(new SubmitBillTenderResultV1("Approved", settlement.ApprovedAmount, null)),
                        CardSettlementOutcome.Declined =>
                            Results.Ok(new SubmitBillTenderResultV1("Declined", null,
                                ((TenderDeclined)cardResult).Reason)),
                        CardSettlementOutcome.RequiresReconciliation =>
                            Results.Ok(new SubmitBillTenderResultV1("RequiresReconciliation", null,
                                ((TenderRequiresReconciliation)cardResult).Reason)),
                        _ => Results.Json(
                            new { error = new { code = "INTERNAL_ERROR", message = "İşlem tamamlanamadı." } },
                            statusCode: StatusCodes.Status500InternalServerError),
                    };
                }
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
            catch (EftUnsettledPaymentExistsException ex)
            {
                return Results.Json(
                    new
                    {
                        error = new
                        {
                            code = "TENDER_UNSETTLED_PAYMENT_EXISTS",
                            message = $"Bu hesapta '{ex.ExistingStatus}' durumunda çözülmemiş bir ödeme var; " +
                                "yeni bir tahsilat eklemeden önce mutabakat tamamlanmalı.",
                        },
                    },
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (EftBillMismatchException)
            {
                return Results.Json(
                    new { error = new { code = "TENDER_IDEMPOTENCY_KEY_REUSED", message = "İşlem kimliği başka bir hesap için zaten kullanılmış." } },
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (CardSettlementUnsettledPaymentExistsException ex)
            {
                return Results.Json(
                    new
                    {
                        error = new
                        {
                            code = "TENDER_UNSETTLED_PAYMENT_EXISTS",
                            message = $"Bu hesapta '{ex.ExistingStatus}' durumunda çözülmemiş bir ödeme var; " +
                                "yeni bir tahsilat eklemeden önce mutabakat tamamlanmalı.",
                        },
                    },
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (CardSettlementBillMismatchException)
            {
                return Results.Json(
                    new { error = new { code = "TENDER_IDEMPOTENCY_KEY_REUSED", message = "İşlem kimliği başka bir hesap için zaten kullanılmış." } },
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (CardSettlementBillNotFoundException)
            {
                return Results.NotFound();
            }
            catch (OverAllocationException ex)
            {
                // V1-RMD-258: a concurrency-triggered over-allocation (the
                // client-side pre-check passed, but a concurrent tender won
                // the real per-bill advisory lock first) must look identical
                // to the cashier as the client-side-detected case — never a
                // raw, unhandled 500 with no Turkish message.
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
            catch (CrossBillPaymentAllocationException)
            {
                return Results.Json(
                    new { error = new { code = "TENDER_IDEMPOTENCY_KEY_REUSED", message = "İşlem kimliği başka bir hesap için zaten kullanılmış." } },
                    statusCode: StatusCodes.Status409Conflict);
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

public sealed record ResolveUnsettledPaymentRequestV1(string? Reason);

public sealed record ResolveUnsettledPaymentResultV1(Guid PaymentId, string Status);

public sealed record SubmitBillTenderResultV1(string Outcome, decimal? ApprovedAmount, string? Reason);

public sealed record PaymentAllocationLineV1(Guid AllocationId, Guid PaymentId, decimal Amount, string PaymentStatus);

/// <summary>V1-RMD-258: a real, persisted unresolved Payment for a bill, if any — the server-side source of truth the client re-derives its lock state from.</summary>
public sealed record UnsettledPaymentV1(Guid PaymentId, string Status, string? Reason);

public sealed record BillTenderSummaryV1(
    Guid BillId,
    decimal PayableAmount,
    decimal AllocatedTotal,
    decimal RemainingAmount,
    IReadOnlyList<PaymentAllocationLineV1> Allocations,
    UnsettledPaymentV1? UnsettledPayment);
