using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.SessionLifecycle;
using ALKAROS.Cash.TenderHandler;
using ALKAROS.Cash.TransactionLedger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ALKAROS.Host.DualScreen;

public static partial class DualScreenApplication
{
    /// <summary>
    /// V13-CSH-004: the first HTTP surface for CashSession (V13-CSH-001),
    /// its ledger (V13-CSH-002) and the cash tender handler (V13-CSH-003) —
    /// all three already existed as isolated domain modules with zero
    /// endpoints, found while starting V13-PUI-002 (a UI cannot call a
    /// backend that was never exposed). Terminal-scoped, same cashier
    /// cookie session every other Cashier-facing route already uses.
    /// </summary>
    public static RouteGroupBuilder MapCashSessionApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/terminals/{terminalId:guid}/cash-sessions")
            .WithTags("CashSession")
            .RequireRateLimiting("terminal-write");

        group.MapGet("/suggested-opening-balance", async (
            Guid terminalId,
            ICashSessionLifecycleService sessions,
            DualScreenStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var suggestion = await sessions.GetSuggestedOpeningBalanceAsync(terminalId, cancellationToken);
            return Results.Ok(new SuggestedOpeningBalanceResponseV1(suggestion));
        });

        group.MapGet("/active", async (
            Guid terminalId,
            ICashSessionRepository sessionRepository,
            DualScreenStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var sessions = await sessionRepository.GetByTerminalIdAsync(terminalId, cancellationToken);
            var active = sessions.FirstOrDefault(s => s.IsActive);
            return active is null ? Results.NotFound() : Results.Ok(active);
        });

        group.MapPost("/", async (
            Guid terminalId,
            OpenCashSessionRequestV1 request,
            ICashSessionLifecycleService sessions,
            ICashTransactionLedgerRepository ledger,
            DualScreenStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var (session, openedEvent) = await sessions.OpenSessionAsync(
                new OpenCashSessionCommand(Guid.NewGuid(), principal.UserId, terminalId, request.OpeningBalance),
                cancellationToken);
            // ICashSessionLifecycleService's own doc: V13-CSH-001 has no
            // ledger of its own - OpenSessionAsync never posts an Opening
            // entry, "once the ledger lands, the caller sums it instead."
            // This composition is that caller: without this, /close's own
            // ComputeExpectedCashAsync would silently start every session
            // at an expected cash of 0 regardless of what was floated.
            if (request.OpeningBalance > 0)
            {
                await ledger.RecordAsync(
                    new Cash.TransactionLedger.CashTransaction(
                        Guid.NewGuid(), session.CashSessionId, CashTransactionType.Opening,
                        request.OpeningBalance, CashTransactionDirection.In,
                        recordedBy: principal.UserId, occurredAt: openedEvent.Timestamp),
                    cancellationToken);
            }
            return Results.Created($"/api/v1/terminals/{terminalId:D}/cash-sessions/{session.CashSessionId:D}", session);
        });

        group.MapPost("/{cashSessionId:guid}/start-count", async (
            Guid terminalId,
            Guid cashSessionId,
            ICashSessionLifecycleService sessions,
            DualScreenStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var (session, _) = await sessions.StartCountAsync(
                new StartCashCountCommand(cashSessionId, principal.UserId), cancellationToken);
            return Results.Ok(session);
        });

        group.MapPost("/{cashSessionId:guid}/counts", async (
            Guid terminalId,
            Guid cashSessionId,
            RecordCashCountRequestV1 request,
            ICashSessionLifecycleService sessions,
            DualScreenStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var recorded = await sessions.RecordCountAsync(
                new RecordCashCountCommand(cashSessionId, request.CountedAmount, principal.UserId, request.Notes),
                cancellationToken);
            return Results.Ok(recorded);
        });

        group.MapPost("/{cashSessionId:guid}/close", async (
            Guid terminalId,
            Guid cashSessionId,
            CloseCashSessionRequestV1 request,
            ICashSessionLifecycleService sessions,
            ICashTransactionLedgerRepository ledger,
            DualScreenStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var expectedCash = await ledger.ComputeExpectedCashAsync(cashSessionId, cancellationToken);
            var (session, closedEvent) = await sessions.CloseSessionAsync(
                new CloseCashSessionCommand(
                    cashSessionId, request.ActualCash, principal.UserId,
                    request.IsSupervisorOverride, request.OverrideReason),
                expectedCash,
                cancellationToken);
            return Results.Ok(new CloseCashSessionResultV1(session, closedEvent.Difference));
        });

        group.MapPost("/{cashSessionId:guid}/reconcile", async (
            Guid terminalId,
            Guid cashSessionId,
            ReconcileCashSessionRequestV1 request,
            ICashSessionLifecycleService sessions,
            DualScreenStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var (session, _) = await sessions.ReconcileSessionAsync(
                new ReconcileCashSessionCommand(cashSessionId, principal.UserId, request.Notes),
                cancellationToken);
            return Results.Ok(session);
        });

        group.MapPost("/{cashSessionId:guid}/cash-tender", async (
            Guid terminalId,
            Guid cashSessionId,
            CashTenderRequestV1 request,
            ICashTenderHandler tenderHandler,
            DualScreenStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var result = await tenderHandler.HandleAsync(
                new CashTenderRequest(
                    cashSessionId, request.BillId, request.AmountDue, request.TenderedAmount,
                    request.IdempotencyKey, principal.UserId),
                cancellationToken);
            return Results.Ok(result);
        });

        return group;
    }
}

/// <summary>V13-CSH-004: request/response DTOs for the CashSession/cash-tender HTTP surface.</summary>
public sealed record SuggestedOpeningBalanceResponseV1(decimal? SuggestedOpeningBalance);

public sealed record OpenCashSessionRequestV1(decimal OpeningBalance);

public sealed record RecordCashCountRequestV1(decimal CountedAmount, string? Notes);

public sealed record CloseCashSessionRequestV1(
    decimal ActualCash, bool IsSupervisorOverride = false, string? OverrideReason = null);

public sealed record CloseCashSessionResultV1(CashSessionSnapshot Session, decimal Difference);

public sealed record ReconcileCashSessionRequestV1(string? Notes);

public sealed record CashTenderRequestV1(
    Guid BillId, decimal AmountDue, decimal TenderedAmount, string IdempotencyKey);
