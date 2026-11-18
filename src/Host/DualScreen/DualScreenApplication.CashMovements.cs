using ALKAROS.Cash.SessionLifecycle;
using ALKAROS.Cash.TransactionLedger;
using ALKAROS.Host.Experience.Billing;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ALKAROS.Host.DualScreen;

public static partial class DualScreenApplication
{
    /// <summary>
    /// the drawer screen's movement list and the decisions on cash-out requests above the approval
    /// threshold. Approving and rejecting need <c>cash.session.override</c>; cancelling is the requester's own.
    /// </summary>
    private static void MapCashMovementRoutes(this RouteGroupBuilder group)
    {
        group.MapGet("/{cashSessionId:guid}/cash-movements", async (
            Guid terminalId,
            Guid cashSessionId,
            ICashSessionRepository sessionRepository,
            CashMovementStore movements,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            return Results.Ok(await movements.ListAsync(cashSessionId, cancellationToken));
        });

        group.MapPost("/{cashSessionId:guid}/cash-movement-requests/{requestId:guid}/approve", async (
            Guid terminalId,
            Guid cashSessionId,
            Guid requestId,
            ApproveCashMovementRequestV1? request,
            ICashSessionRepository sessionRepository,
            CashMovementStore movements,
            DualScreenStore store,
            IAuthorizationService authorization,
            InPlaceManagerApproval approval,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            // With a typed manager sign-in the cashier at the till only needs the drawer, and the request has to be the
            // cashier's own; without one the caller's own override permission decides, as before.
            if (request?.ManagerUsername is null && request?.ManagerPassword is null)
            {
                var approver = await RequireCashierPermissionAsync(
                    context, terminalId, store, authorization, ApplicationPermissions.CashSessionOverride, cancellationToken);
                var session = await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
                return Results.Ok(await movements.ApproveAsync(cashSessionId, requestId, approver.UserId, session.Snapshot.CashierUserId, cancellationToken));
            }

            var cashier = await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            var manager = await approval.VerifyAsync(
                request!.ManagerUsername, request.ManagerPassword, cashier.UserId, ApplicationPermissions.CashSessionOverride,
                "cash.movement_request.approval_failed", "CashSession", cashSessionId, context.TraceIdentifier, cancellationToken);
            var approved = await movements.ApproveAsync(cashSessionId, requestId, manager.UserId, cashier.UserId, cancellationToken);
            approval.Confirm(cashier.UserId, request.ManagerUsername!);
            return Results.Ok(approved);
        });

        group.MapPost("/{cashSessionId:guid}/cash-movement-requests/{requestId:guid}/reject", async (
            Guid terminalId,
            Guid cashSessionId,
            Guid requestId,
            RejectCashMovementRequestV1? request,
            ICashSessionRepository sessionRepository,
            CashMovementStore movements,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierPermissionAsync(
                context, terminalId, store, authorization, ApplicationPermissions.CashSessionOverride, cancellationToken);
            await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            return Results.Ok(await movements.RejectAsync(cashSessionId, requestId, principal.UserId, request?.Note, cancellationToken));
        });

        group.MapPost("/{cashSessionId:guid}/cash-movement-requests/{requestId:guid}/cancel", async (
            Guid terminalId,
            Guid cashSessionId,
            Guid requestId,
            ICashSessionRepository sessionRepository,
            CashMovementStore movements,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            return Results.Ok(await movements.CancelAsync(cashSessionId, requestId, principal.UserId, cancellationToken));
        });
    }
}

/// <summary>The manager's own sign-in when a manager approves a cash-out request at the cashier's till.</summary>
public sealed record ApproveCashMovementRequestV1(string? ManagerUsername, string? ManagerPassword);

/// <summary>the optional note a manager gives when rejecting a cash-out request.</summary>
public sealed record RejectCashMovementRequestV1(string? Note);
