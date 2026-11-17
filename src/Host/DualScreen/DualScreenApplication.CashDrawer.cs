using ALKAROS.Cash.SessionLifecycle;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ALKAROS.Host.DualScreen;

public static partial class DualScreenApplication
{
    /// <summary>The physical drawer: whether the till may show "no sale", and opening the drawer without a sale.</summary>
    private static void MapCashDrawerRoutes(this RouteGroupBuilder group)
    {
        group.MapGet("/{cashSessionId:guid}/cash-drawer", async (
            Guid terminalId,
            Guid cashSessionId,
            ICashSessionRepository sessionRepository,
            CashDrawerService drawer,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            return Results.Ok(await drawer.StatusAsync(cashSessionId, principal.UserId, cancellationToken));
        });

        group.MapPost("/{cashSessionId:guid}/cash-drawer/no-sale", async (
            Guid terminalId,
            Guid cashSessionId,
            NoSaleDrawerRequestV1 request,
            ICashSessionRepository sessionRepository,
            CashDrawerService drawer,
            DualScreenStore store,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashDrawerAsync(context, terminalId, store, authorization, cancellationToken);
            var session = await RequireSessionOnTerminalAsync(sessionRepository, cashSessionId, terminalId, cancellationToken);
            await authorization.AuthorizeAsync(principal.UserId, ApplicationPermissions.CashDrawerKick, cancellationToken);
            // The drawer belongs to the cashier whose session it is, the same rule as taking cash into it.
            if (session.Snapshot.CashierUserId != principal.UserId)
                throw new CashTenderSessionNotOwnedException();
            return Results.Ok(await drawer.OpenWithoutSaleAsync(cashSessionId, terminalId, principal.UserId, request, cancellationToken));
        });
    }
}
