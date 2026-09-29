using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.SessionLifecycle;
using ALKAROS.Cash.TenderHandler;
using ALKAROS.Cash.TransactionLedger;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

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
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            var suggestion = await sessions.GetSuggestedOpeningBalanceAsync(terminalId, cancellationToken);
            return Results.Ok(new SuggestedOpeningBalanceResponseV1(suggestion));
        });

        group.MapGet("/active", async (
            Guid terminalId,
            ICashSessionRepository sessionRepository,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            var sessions = await sessionRepository.GetByTerminalIdAsync(terminalId, cancellationToken);
            var active = sessions.FirstOrDefault(s => s.IsActive);
            return active is null ? Results.NotFound() : Results.Ok(active);
        });

        group.MapPost("/", async (
            Guid terminalId,
            OpenCashSessionRequestV1 request,
            ICashSessionLifecycleService sessions,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            // The Opening ledger entry is what /close's ComputeExpectedCashAsync starts from; it is written in the
            // same transaction as the session (V1-RMD-416), never as a second write that could fail on its own.
            var (session, _) = await sessions.OpenSessionWithOpeningEntryAsync(
                new OpenCashSessionCommand(Guid.NewGuid(), principal.UserId, terminalId, request.OpeningBalance),
                cancellationToken);
            return Results.Created($"/api/v1/terminals/{terminalId:D}/cash-sessions/{session.CashSessionId:D}", session);
        });

        group.MapPost("/{cashSessionId:guid}/start-count", async (
            Guid terminalId,
            Guid cashSessionId,
            ICashSessionLifecycleService sessions,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            ICashSessionRepository sessionRepository,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
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
            IAuthorizationService authorization,
            HttpContext context,
            ICashSessionRepository sessionRepository,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            var recorded = await sessions.RecordCountAsync(
                new RecordCashCountCommand(cashSessionId, request.CountedAmount, principal.UserId, request.Notes),
                cancellationToken);
            return Results.Ok(recorded);
        });

        group.MapGet("/{cashSessionId:guid}/expected-cash", async (
            Guid terminalId,
            Guid cashSessionId,
            ICashTransactionLedgerRepository ledger,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            ICashSessionRepository sessionRepository,
            CancellationToken cancellationToken) =>
        {
            await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            var expectedCash = await ledger.ComputeExpectedCashAsync(cashSessionId, cancellationToken);
            return Results.Ok(new ExpectedCashResponseV1(expectedCash));
        });

        group.MapPost("/{cashSessionId:guid}/close", async (
            Guid terminalId,
            Guid cashSessionId,
            CloseCashSessionRequestV1 request,
            ICashSessionLifecycleService sessions,
            ICashTransactionLedgerRepository ledger,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            ICashSessionRepository sessionRepository,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            // V1-RMD-236: IsSupervisorOverride bypasses CashSessionPolicy's own
            // variance-tolerance check below — without this, any cashier could
            // self-declare the override and close with an unlimited variance.
            if (request.IsSupervisorOverride)
            {
                await authorization.AuthorizeAsync(
                    principal.UserId, ApplicationPermissions.CashSessionOverride, cancellationToken);
            }
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
            ICashSessionRepository sessionRepository,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            // V1-RMD-400 (V1-RMD-393 F-09): reconciliation is a supervisor act (cash-session-design.md §6) and a
            // four-eyes check — the cashier who ran the drawer never signs off their own session.
            await authorization.AuthorizeAsync(
                principal.UserId, ApplicationPermissions.CashSessionOverride, cancellationToken);
            var reconciled = await sessionRepository.GetByIdAsync(cashSessionId, cancellationToken);
            if (reconciled is not null && reconciled.Snapshot.CashierUserId == principal.UserId)
            {
                throw new AuthorizationDeniedException(
                    principal.UserId, ApplicationPermissions.CashSessionOverride, "The session's own cashier cannot reconcile it.");
            }
            var (session, _) = await sessions.ReconcileSessionAsync(
                new ReconcileCashSessionCommand(cashSessionId, principal.UserId, request.Notes),
                cancellationToken);
            return Results.Ok(session);
        });

        group.MapPost("/{cashSessionId:guid}/cash-movements", async (
            Guid terminalId,
            Guid cashSessionId,
            RecordCashMovementRequestV1 request,
            ICashSessionRepository sessionRepository,
            ICashTransactionLedgerRepository ledger,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
                throw new ArgumentException("IdempotencyKey is required.", nameof(request));

            // V1-RMD-241: a network retry (or a race just under the client's
            // own double-click guard) used to always insert a brand-new row
            // here, unlike cash-tender's own idempotency-key handling.
            var replay = await ledger.GetBySessionAndIdempotencyKeyAsync(cashSessionId, request.IdempotencyKey, cancellationToken);
            if (replay is not null)
            {
                return Results.Created(
                    $"/api/v1/terminals/{terminalId:D}/cash-sessions/{cashSessionId:D}/cash-movements/{replay.Id:D}",
                    replay);
            }

            var session = await sessionRepository.GetByIdAsync(cashSessionId, cancellationToken)
                ?? throw new CashSessionNotFoundException(cashSessionId);
            if (session.Snapshot.Status != CashSessionStatus.Open)
            {
                throw new ClosedCashSessionException(cashSessionId, session.Snapshot.Status);
            }
            var transactionType = request.Direction == CashMovementDirectionV1.In
                ? CashTransactionType.CashIn
                : CashTransactionType.CashOut;
            var direction = request.Direction == CashMovementDirectionV1.In
                ? CashTransactionDirection.In
                : CashTransactionDirection.Out;
            var movement = new Cash.TransactionLedger.CashTransaction(
                Guid.NewGuid(), cashSessionId, transactionType, request.Amount, direction,
                recordedBy: principal.UserId, notes: request.Notes, idempotencyKey: request.IdempotencyKey);
            try
            {
                await ledger.RecordAsync(movement, cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                // A concurrent request for the same key won the race between
                // the check above and this insert; its row is the real one.
                var raced = await ledger.GetBySessionAndIdempotencyKeyAsync(cashSessionId, request.IdempotencyKey, cancellationToken);
                if (raced is null)
                    throw;
                return Results.Created(
                    $"/api/v1/terminals/{terminalId:D}/cash-sessions/{cashSessionId:D}/cash-movements/{raced.Id:D}",
                    raced);
            }
            return Results.Created(
                $"/api/v1/terminals/{terminalId:D}/cash-sessions/{cashSessionId:D}/cash-movements/{movement.Id:D}",
                movement);
        });

        group.MapPost("/{cashSessionId:guid}/cash-tender", async (
            Guid terminalId,
            Guid cashSessionId,
            CashTenderRequestV1 request,
            ICashTenderHandler tenderHandler,
            ALKAROS.Billing.PaymentClosure.IBillClosureService billClosure,
            ALKAROS.Host.Experience.Orders.OrderSettlementService orderSettlement,
            DualScreenStore store,
            IAuthorizationService authorization,
            IHubContext<CustomerDisplayHub> customerDisplayHub,
            HttpContext context,
            ICashSessionRepository sessionRepository,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            // V1-RMD-401: a cash tender is a payment too — cash.drawer (the money enters the drawer) and payments.take.
            await authorization.AuthorizeAsync(principal.UserId, ApplicationPermissions.PaymentsTake, cancellationToken);
            var result = await tenderHandler.HandleAsync(
                new CashTenderRequest(
                    cashSessionId, request.BillId, request.AmountDue, request.TenderedAmount,
                    request.IdempotencyKey, principal.UserId),
                cancellationToken);
            // V1-RMD-276: a cash tender that covers the bill completes it (a replay of the same key stays a no-op).
            // V1-RMD-294: and (best effort) pokes the paired customer display to refresh.
            await TryCloseBillAsync(billClosure, orderSettlement, request.BillId, customerDisplayHub, terminalId, cancellationToken);
            return Results.Ok(result);
        });

        return group;
    }

    /// <summary>
    /// V1-RMD-411 (V1-RMD-393 F-08): a drawer session belongs to one terminal. A cashier signed in on terminal B must
    /// not count, close, reconcile, move cash in or out of, or sell into terminal A's session by naming its id in B's
    /// route; a foreign session answers exactly like an unknown one.
    /// </summary>
    private static async Task RequireSessionOnTerminalAsync(
        ICashSessionRepository sessionRepository, Guid cashSessionId, Guid terminalId, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetByIdAsync(cashSessionId, cancellationToken);
        if (session is not null && session.Snapshot.TerminalId != terminalId)
            throw new CashSessionNotFoundException(cashSessionId);
    }

    /// <summary>
    /// V1-RMD-400 (V1-RMD-399 H-02): every drawer action needs <c>cash.drawer</c> (authorization model §2/§3),
    /// not only a signed-in device — a waiter or kitchen session must not open, count or pay out of a drawer.
    /// </summary>
    private static Task<CashierPrincipal> RequireCashDrawerAsync(
        HttpContext context,
        Guid terminalId,
        DualScreenStore store,
        IAuthorizationService authorization,
        CancellationToken cancellationToken)
        => RequireCashierPermissionAsync(
            context, terminalId, store, authorization, ApplicationPermissions.CashDrawer, cancellationToken);
}

/// <summary>V13-CSH-004: request/response DTOs for the CashSession/cash-tender HTTP surface.</summary>
public sealed record SuggestedOpeningBalanceResponseV1(decimal? SuggestedOpeningBalance);

public sealed record OpenCashSessionRequestV1(decimal OpeningBalance);

public sealed record RecordCashCountRequestV1(decimal CountedAmount, string? Notes);

public sealed record CloseCashSessionRequestV1(
    decimal ActualCash, bool IsSupervisorOverride = false, string? OverrideReason = null);

public sealed record CloseCashSessionResultV1(CashSessionSnapshot Session, decimal Difference);

/// <summary>
/// V13-CSH-004 (Ek, 2026-09-18): CashSessionSnapshot.ExpectedCash is only
/// ever refreshed by CloseSessionAsync's own write path - before a close is
/// actually committed it still holds the value from Open (V13-PUI-002's own
/// Fark Teyidi screen found this live, showing a wrong "Beklenen" figure).
/// This is a read-only preview of the same ComputeExpectedCashAsync the
/// close endpoint itself uses, so the confirmation screen can show the real
/// number before the cashier commits.
/// </summary>
public sealed record ExpectedCashResponseV1(decimal ExpectedCash);

public sealed record ReconcileCashSessionRequestV1(string? Notes);

public sealed record CashTenderRequestV1(
    Guid BillId, decimal AmountDue, decimal TenderedAmount, string IdempotencyKey);

/// <summary>V13-CSH-004: manual cash-in/cash-out drawer movement, outside any sale.</summary>
public enum CashMovementDirectionV1
{
    In,
    Out,
}

public sealed record RecordCashMovementRequestV1(
    CashMovementDirectionV1 Direction, decimal Amount, string? Notes, string IdempotencyKey);
