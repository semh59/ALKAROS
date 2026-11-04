using ALKAROS.Audit.EventStore;
using ALKAROS.Payments.Allocations.Persistence;
using Microsoft.Extensions.Logging;
using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.SessionLifecycle;
using ALKAROS.Cash.TenderHandler;
using ALKAROS.Cash.TransactionLedger;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Settings.CashCloseTolerance;
using ALKAROS.Settings.ManagerSelfApproval;
using ALKAROS.Settings.TypedSettings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;

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
            var active = await sessionRepository.GetActiveByTerminalIdAsync(terminalId, cancellationToken) is [var only] ? only : null;
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
            CashMovementStore movements,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            ICashSessionRepository sessionRepository,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            var record = await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            var mayActForOwner = await MayActForOwnerAsync(record, principal.UserId, authorization, cancellationToken);
            // requests are decided before counting starts; nothing can be requested once the session leaves Open.
            if (await movements.HasPendingAsync(cashSessionId, cancellationToken))
                throw new PendingCashMovementRequestsException(cashSessionId);
            var (session, _) = await sessions.StartCountAsync(
                new StartCashCountCommand(cashSessionId, principal.UserId, mayActForOwner), cancellationToken);
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
            var record = await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            var mayActForOwner = await MayActForOwnerAsync(record, principal.UserId, authorization, cancellationToken);
            var recorded = await sessions.RecordCountAsync(
                new RecordCashCountCommand(cashSessionId, request.CountedAmount, principal.UserId, request.Notes, mayActForOwner),
                cancellationToken);
            return Results.Ok(recorded);
        });

        group.MapGet("/{cashSessionId:guid}/expected-cash", async (
            Guid terminalId,
            Guid cashSessionId,
            ICashTransactionLedgerRepository ledger,
            CashCloseStore closeStore,
            ISettingsService settings,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            ICashSessionRepository sessionRepository,
            CancellationToken cancellationToken) =>
        {
            await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            var expectedCash = await ledger.ComputeExpectedCashAsync(cashSessionId, cancellationToken);
            var unsettled = await closeStore.ReadUnsettledAsync(cancellationToken);
            return Results.Ok(new ExpectedCashResponseV1(
                expectedCash, await CloseVarianceToleranceSetting.GetToleranceAsync(settings, cancellationToken),
                unsettled.OpenBills, unsettled.UnsettledPayments));
        });

        group.MapPost("/{cashSessionId:guid}/close", async (
            Guid terminalId,
            Guid cashSessionId,
            CloseCashSessionRequestV1 request,
            ICashSessionLifecycleService sessions,
            CashMovementStore movements,
            CashCloseStore closeStore,
            ISettingsService settings,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            ICashSessionRepository sessionRepository,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            var record = await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            var mayActForOwner = await MayActForOwnerAsync(record, principal.UserId, authorization, cancellationToken);
            // start-count already refuses open requests and none can be opened once the session left Open;
            // this guards the one request that was committed in the same instant as the count started.
            if (await movements.HasPendingAsync(cashSessionId, cancellationToken))
                throw new PendingCashMovementRequestsException(cashSessionId);
            // V1-RMD-236: IsSupervisorOverride bypasses CashSessionPolicy's own
            // variance-tolerance check below — without this, any cashier could
            // self-declare the override and close with an unlimited variance.
            if (request.IsSupervisorOverride)
            {
                await authorization.AuthorizeAsync(
                    principal.UserId, ApplicationPermissions.CashSessionOverride, cancellationToken);
            }
            var tolerance = await CloseVarianceToleranceSetting.GetToleranceAsync(settings, cancellationToken);
            // The expected cash and the unsettled counts are read by the close itself under the session lock; the
            // unacknowledged-warning check and the audit event run inside the same transaction, after every rule of the close was judged.
            var (session, closedEvent) = await sessions.CloseSessionAsync(
                new CloseCashSessionCommand(
                    cashSessionId, request.ActualCash, principal.UserId,
                    request.IsSupervisorOverride, request.OverrideReason, request.DifferenceExplanation, mayActForOwner),
                tolerance,
                (closed, locked, token) => closeStore.AuditClosedAsync(closed, request.AcknowledgeUnsettled, tolerance, locked, token),
                cancellationToken);
            return Results.Ok(new CloseCashSessionResultV1(session, closedEvent.Difference));
        });

        group.MapPost("/{cashSessionId:guid}/reconcile", async (
            Guid terminalId,
            Guid cashSessionId,
            ReconcileCashSessionRequestV1 request,
            ICashSessionLifecycleService sessions,
            ICashSessionRepository sessionRepository,
            CashCloseStore closeStore,
            ISettingsService settings,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            // Reconciliation is a manager act (cash.session.override) and a four-eyes
            // check - the cashier who ran the drawer and the person who closed it do not sign it off, unless the shared
            // self-approval setting is on (then the record is marked).
            await authorization.AuthorizeAsync(
                principal.UserId, ApplicationPermissions.CashSessionOverride, cancellationToken);
            var selfReconcileAllowed = await ManagerSelfApprovalSetting.IsEnabledAsync(settings, cancellationToken);
            var (session, _) = await sessions.ReconcileSessionAsync(
                new ReconcileCashSessionCommand(cashSessionId, principal.UserId, request.Notes, selfReconcileAllowed),
                (reconciled, locked, token) => closeStore.AuditReconciledAsync(reconciled, locked, token),
                cancellationToken);
            return Results.Ok(session);
        });

        group.MapPost("/{cashSessionId:guid}/cash-movements", async (
            Guid terminalId,
            Guid cashSessionId,
            RecordCashMovementRequestV1 request,
            ICashSessionRepository sessionRepository,
            CashMovementStore movements,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.IdempotencyKey);
            var direction = request.Direction == CashMovementDirectionV1.In ? CashTransactionDirection.In : CashTransactionDirection.Out;

            var outcome = await movements.RecordAsync(
                cashSessionId, principal.UserId, direction, request.Amount, request.Notes, request.IdempotencyKey, cancellationToken);
            // 202 while the request still waits; a replay of one that was already decided is a plain 200.
            return outcome.Requested is { } waiting
                ? Results.Json(
                    waiting,
                    statusCode: waiting.Status == CashMovementRequestStatus.Requested ? StatusCodes.Status202Accepted : StatusCodes.Status200OK)
                : Results.Created(
                    $"/api/v1/terminals/{terminalId:D}/cash-sessions/{cashSessionId:D}/cash-movements/{outcome.Recorded!.Id:D}",
                    outcome.Recorded);
        });

        group.MapPost("/{cashSessionId:guid}/cash-tender", async (
            Guid terminalId,
            Guid cashSessionId,
            CashTenderRequestV1 request,
            ICashTenderHandler tenderHandler,
            IAuditEventStore auditEvents,
            ALKAROS.Billing.PaymentClosure.IBillClosureService billClosure,
            ALKAROS.Host.Experience.Orders.OrderSettlementService orderSettlement,
            DualScreenStore store,
            IAuthorizationService authorization,
            IHubContext<CustomerDisplayHub> customerDisplayHub,
            ILoggerFactory loggerFactory,
            HttpContext context,
            ICashSessionRepository sessionRepository,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            // V1-RMD-401: a cash tender is a payment too — cash.drawer (the money enters the drawer) and payments.take.
            await authorization.AuthorizeAsync(principal.UserId, ApplicationPermissions.PaymentsTake, cancellationToken);
            CashTenderResult result;
            try
            {
                result = await tenderHandler.HandleAsync(
                    new CashTenderRequest(
                        cashSessionId, request.BillId, request.AmountDue, request.TenderedAmount,
                        request.IdempotencyKey, principal.UserId),
                    cancellationToken);
            }
            catch (OverAllocationException exception)
            {
                await RecordRejectedTenderAsync(
                    auditEvents, loggerFactory, context, principal.UserId, terminalId, request.BillId, "Cash",
                    exception.RequestedAmount, exception.RemainingPayable, request.IdempotencyKey);
                throw;
            }
            // V1-RMD-276: a cash tender that covers the bill completes it (a replay of the same key stays a no-op).
            // V1-RMD-294: and (best effort) pokes the paired customer display to refresh.
            await TryCloseBillAsync(billClosure, orderSettlement, request.BillId, customerDisplayHub, loggerFactory, terminalId, cancellationToken);
            return Results.Ok(result);
        });

        group.MapCashMovementRoutes();

        return group;
    }

    /// <summary>
    /// V1-RMD-411 (V1-RMD-393 F-08): a drawer session belongs to one terminal. A cashier signed in on terminal B must
    /// not count, close, reconcile, move cash in or out of, or sell into terminal A's session by naming its id in B's
    /// route; a foreign session answers exactly like an unknown one.
    /// </summary>
    private static async Task<CashSessionRecord> RequireSessionOnTerminalAsync(
        ICashSessionRepository sessionRepository, Guid cashSessionId, Guid terminalId, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetByIdAsync(cashSessionId, cancellationToken);
        if (session is null || session.Snapshot.TerminalId != terminalId)
            throw new CashSessionNotFoundException(cashSessionId);
        return session;
    }

    /// <summary>
    /// A drawer session is counted and closed by the cashier who opened it; a manager (<c>cash.session.override</c>)
    /// may act for them. The permission is asked only of someone else, so the cashier's own close never writes a
    /// denial record.
    /// </summary>
    private static async Task<bool> MayActForOwnerAsync(
        CashSessionRecord session, Guid userId, IAuthorizationService authorization, CancellationToken cancellationToken)
    {
        if (session.Snapshot.CashierUserId == userId)
            return false;
        try
        {
            await authorization.AuthorizeAsync(userId, ApplicationPermissions.CashSessionOverride, cancellationToken);
            return true;
        }
        catch (AuthorizationDeniedException)
        {
            return false;
        }
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

/// <summary>
/// <paramref name="DifferenceExplanation"/> is required whenever the counted cash differs from the expected cash;
/// <paramref name="AcknowledgeUnsettled"/> is the cashier's deliberate "close anyway" when open bills or uncertain payments exist.
/// </summary>
public sealed record CloseCashSessionRequestV1(
    decimal ActualCash, bool IsSupervisorOverride = false, string? OverrideReason = null,
    string? DifferenceExplanation = null, bool AcknowledgeUnsettled = false);

public sealed record CloseCashSessionResultV1(CashSessionSnapshot Session, decimal Difference);

/// <summary>
/// V13-CSH-004 (Ek, 2026-09-18): CashSessionSnapshot.ExpectedCash is only
/// ever refreshed by CloseSessionAsync's own write path - before a close is
/// actually committed it still holds the value from Open (V13-PUI-002's own
/// Fark Teyidi screen found this live, showing a wrong "Beklenen" figure).
/// This is a read-only preview of the same ComputeExpectedCashAsync the
/// close endpoint itself uses, so the confirmation screen can show the real
/// number before the cashier commits. It also carries what the close screen needs besides the number: the manager-set
/// difference tolerance and the business-wide counts of open bills and uncertain payments the close warns about.
/// </summary>
public sealed record ExpectedCashResponseV1(
    decimal ExpectedCash, decimal VarianceTolerance, int OpenBillCount, int UnsettledPaymentCount);

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
