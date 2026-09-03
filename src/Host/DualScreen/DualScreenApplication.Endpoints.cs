using System.Net;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ALKAROS.Identity.Authentication;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Kitchen.Routing;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using ALKAROS.Host.Composition;
using ALKAROS.Host.Composition.Modules;
using ALKAROS.Host.Experience.Billing;
using ALKAROS.Host.Experience.Catalog;
using ALKAROS.Host.Experience.KitchenOperations;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Host.Experience.Tables;
using ALKAROS.Host.Outbox;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.DualScreen;

public static partial class DualScreenApplication
{
    private static void MapApi(WebApplication app)
    {
        app.MapGet("/health/ready", async (DualScreenStore store, CancellationToken cancellationToken) =>
        {
            await store.CheckReadyAsync(cancellationToken);
            return Results.Ok(new { status = "Ready" });
        });

        app.MapPost("/api/v1/auth/login", async (
            LoginRequest request,
            HttpContext context,
            AuthenticationService authentication,
            IDeviceSessionService sessions,
            DualScreenStore store,
            IRoleRepository roles,
            CancellationToken cancellationToken) =>
        {
            if (request.TerminalId == Guid.Empty
                || string.IsNullOrWhiteSpace(request.Username)
                || string.IsNullOrEmpty(request.Password))
            {
                return Error(context, StatusCodes.Status400BadRequest, "VALIDATION_FAILED", "Giriş bilgileri geçersiz.");
            }

            var result = await authentication.LoginAsync(
                request.Username.Trim(), request.Password, DateTimeOffset.UtcNow, cancellationToken);
            if (result is not LoginSuccess success)
                return Error(context, StatusCodes.Status401Unauthorized, "INVALID_CREDENTIALS", "Kullanıcı adı veya parola hatalı.");

            var deviceId = $"cashier:{request.TerminalId:D}";
            await sessions.RevokeDeviceAsync(success.UserId, deviceId, cancellationToken);
            var (session, rawToken) = await sessions.CreateSessionAsync(
                success.UserId, deviceId, TimeSpan.FromHours(12), cancellationToken);
            await store.EnsureTerminalAsync(request.TerminalId, cancellationToken);
            AppendCookie(context, CashierCookieName, rawToken, session.ExpiresAt);
            var capabilities = await roles.GetPermissionCodesForUserAsync(success.UserId, cancellationToken);
            await sessions.RevokeDeviceAsync(success.UserId, $"manager:{request.TerminalId:D}", cancellationToken);
            if (capabilities.Contains(CatalogManagementEndpoints.ManagePermission, StringComparer.Ordinal))
            {
                var (managerSession, managerToken) = await sessions.CreateSessionAsync(
                    success.UserId, $"manager:{request.TerminalId:D}", TimeSpan.FromHours(12), cancellationToken);
                AppendCookie(context, CatalogManagementEndpoints.ManagerCookieName, managerToken, managerSession.ExpiresAt);
            }
            else
            {
                context.Response.Cookies.Delete(CatalogManagementEndpoints.ManagerCookieName);
            }

            return Results.Ok(new
            {
                userId = success.UserId,
                displayName = success.DisplayName,
                terminalId = request.TerminalId,
                capabilities,
            });
        }).RequireRateLimiting("login");

        app.MapGet("/api/v1/auth/session", async (
            Guid terminalId,
            HttpContext context,
            DualScreenStore store,
            IRoleRepository roles,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var capabilities = await roles.GetPermissionCodesForUserAsync(principal.UserId, cancellationToken);
            return Results.Ok(new
            {
                userId = principal.UserId,
                displayName = principal.DisplayName,
                terminalId,
                capabilities,
            });
        }).RequireRateLimiting("terminal-read");

        app.MapGet("/api/v1/auth/session/current", async (
            HttpContext context,
            DualScreenStore store,
            IRoleRepository roles,
            CancellationToken cancellationToken) =>
        {
            var cashierToken = context.Request.Cookies[CashierCookieName];
            var principal = await store.AuthenticateCashierByCookieAsync(cashierToken, cancellationToken);
            if (principal is null)
                throw new DualScreenUnauthorizedException("Cashier authentication is required.");
            var capabilities = await roles.GetPermissionCodesForUserAsync(principal.UserId, cancellationToken);
            return Results.Ok(new
            {
                userId = principal.UserId,
                displayName = principal.DisplayName,
                terminalId = principal.TerminalId,
                capabilities,
            });
        }).RequireRateLimiting("terminal-read");

        app.MapGet("/api/v1/terminals/{terminalId:guid}/runtime-configuration", async (
            Guid terminalId,
            HttpContext context,
            DualScreenStore store,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var kitchenStationId = Environment.GetEnvironmentVariable(KitchenStationEnvironmentVariable)?.Trim();
            if (string.IsNullOrWhiteSpace(kitchenStationId))
            {
                throw new InvalidOperationException(
                    $"{KitchenStationEnvironmentVariable} is required before kitchen operations are enabled.");
            }

            return Results.Ok(new { kitchenStationId });
        }).RequireRateLimiting("terminal-read");

        app.MapPost("/api/v1/auth/logout", async (
            Guid terminalId,
            HttpContext context,
            IDeviceSessionService sessions,
            DualScreenStore store,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierAsync(context, terminalId, store, cancellationToken);
            await sessions.RevokeAsync(principal.SessionId, cancellationToken);
            await sessions.RevokeDeviceAsync(principal.UserId, $"manager:{terminalId:D}", cancellationToken);
            context.Response.Cookies.Delete(CashierCookieName);
            context.Response.Cookies.Delete(CatalogManagementEndpoints.ManagerCookieName);
            return Results.NoContent();
        }).RequireRateLimiting("terminal-write");

        app.MapGet("/api/v1/terminals/{terminalId:guid}/catalog", async (
            Guid terminalId,
            string? category,
            string? limit,
            string? cursor,
            HttpContext context,
            DualScreenStore store,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var pageSize = DualScreenStore.ParseCatalogLimit(limit);
            var page = await store.GetCatalogAsync(category, pageSize, cursor, cancellationToken);
            context.Response.Headers["X-Catalog-Limit"] = pageSize.ToString(CultureInfo.InvariantCulture);
            if (page.NextCursor is not null)
                context.Response.Headers["X-Next-Cursor"] = page.NextCursor;
            return Results.Ok(page.Items);
        }).RequireRateLimiting("terminal-read");

        app.MapPost("/api/v1/terminals/{terminalId:guid}/orders", async (
            Guid terminalId,
            HttpContext context,
            DualScreenStore store,
            IAuthorizationService authorization,
            IHubContext<CustomerDisplayHub> hub,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, store, authorization, cancellationToken);
            var created = await store.StartOrderAsync(terminalId, cancellationToken);
            await NotifyAsync(hub, terminalId, created.OrderId, created.Revision, cancellationToken);
            return Results.Created($"/api/v1/terminals/{terminalId:D}/orders/{created.OrderId:D}", created);
        }).RequireRateLimiting("terminal-write");

        app.MapPost("/api/v1/terminals/{terminalId:guid}/orders/table", async (
            Guid terminalId,
            StartOrderRequest request,
            HttpContext context,
            DualScreenStore store,
            IAuthorizationService authorization,
            IHubContext<CustomerDisplayHub> hub,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, store, authorization, cancellationToken);
            if (request.TableId is null)
                throw new ArgumentException("TableId is required for a table order.", nameof(request));
            var created = await store.StartOrderAsync(terminalId, request, cancellationToken);
            await NotifyAsync(hub, terminalId, created.OrderId, created.Revision, cancellationToken);
            return Results.Created($"/api/v1/terminals/{terminalId:D}/orders/{created.OrderId:D}", created);
        }).RequireRateLimiting("terminal-write");

        app.MapGet("/api/v1/terminals/{terminalId:guid}/orders/active", async (
            Guid terminalId,
            HttpContext context,
            DualScreenStore store,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var displayId = await store.GetActiveDisplayIdAsync(terminalId, cancellationToken) ?? Guid.Empty;
            return Results.Ok(await store.GetSnapshotAsync(displayId, terminalId, cancellationToken));
        }).RequireRateLimiting("terminal-read");

        app.MapPost("/api/v1/terminals/{terminalId:guid}/orders/{orderId:guid}/items", async (
            Guid terminalId,
            Guid orderId,
            AddOrderItemRequest request,
            HttpContext context,
            DualScreenStore store,
            IAuthorizationService authorization,
            IHubContext<CustomerDisplayHub> hub,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, store, authorization, cancellationToken);
            var mutation = await store.AddItemAsync(terminalId, orderId, request, cancellationToken);
            await NotifyAsync(hub, terminalId, orderId, mutation.Revision, cancellationToken);
            return Results.Ok(mutation);
        }).RequireRateLimiting("terminal-write");

        app.MapPatch("/api/v1/terminals/{terminalId:guid}/orders/{orderId:guid}/items/{itemId:guid}", async (
            Guid terminalId,
            Guid orderId,
            Guid itemId,
            ChangeOrderItemQuantityRequest request,
            HttpContext context,
            DualScreenStore store,
            IAuthorizationService authorization,
            IHubContext<CustomerDisplayHub> hub,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, store, authorization, cancellationToken);
            var mutation = await store.ChangeItemQuantityAsync(
                terminalId, orderId, itemId, request, cancellationToken);
            await NotifyAsync(hub, terminalId, orderId, mutation.Revision, cancellationToken);
            return Results.Ok(mutation);
        }).RequireRateLimiting("terminal-write");

        app.MapDelete("/api/v1/terminals/{terminalId:guid}/orders/{orderId:guid}/items/{itemId:guid}", async (
            Guid terminalId,
            Guid orderId,
            Guid itemId,
            long expectedRevision,
            HttpContext context,
            DualScreenStore store,
            IAuthorizationService authorization,
            IHubContext<CustomerDisplayHub> hub,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, store, authorization, cancellationToken);
            var mutation = await store.RemoveItemAsync(
                terminalId, orderId, itemId, expectedRevision, cancellationToken);
            await NotifyAsync(hub, terminalId, orderId, mutation.Revision, cancellationToken);
            return Results.Ok(mutation);
        }).RequireRateLimiting("terminal-write");

        app.MapPost("/api/v1/terminals/{terminalId:guid}/orders/{orderId:guid}/submit", async (
            Guid terminalId,
            Guid orderId,
            SubmitOrderRequest request,
            HttpContext context,
            DualScreenStore store,
            IAuthorizationService authorization,
            SubmitOrderHandler handler,
            IHubContext<CustomerDisplayHub> hub,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierPermissionAsync(
                context, terminalId, store, authorization, cancellationToken);
            var result = await handler.HandleAsync(new SubmitOrderCommand(
                $"cashier:{terminalId:D}", request.OperationId, orderId, request.ExpectedRevision,
                principal.UserId, DateTimeOffset.UtcNow, "Cashier submitted order"), cancellationToken);
            await NotifyAsync(hub, terminalId, orderId, result.RowVersion, cancellationToken);
            return Results.Ok(result);
        }).RequireRateLimiting("terminal-write");

        app.MapPost("/api/v1/customer-displays/pairing-requests", async (
            PairingRequestCreate request,
            DualScreenStore store,
            CancellationToken cancellationToken) =>
            Results.Created(
                $"/api/v1/customer-displays/pairing-requests/{request.DisplayId:D}",
            await store.CreatePairingRequestAsync(request.DisplayId, cancellationToken)))
            .RequireRateLimiting("pairing-create");

        app.MapPost("/api/v1/terminals/{terminalId:guid}/pairings/approve", async (
            Guid terminalId,
            PairingApprovalRequest request,
            HttpContext context,
            DualScreenStore store,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, store, authorization, cancellationToken);
            await store.ApprovePairingAsync(terminalId, request.Code, cancellationToken);
            return Results.NoContent();
        }).RequireRateLimiting("pairing-approve");

        app.MapPost("/api/v1/customer-displays/pairing-requests/{requestId:guid}/complete", async (
            Guid requestId,
            PairingCompletionRequest request,
            HttpContext context,
            DualScreenStore store,
            CancellationToken cancellationToken) =>
        {
            var completion = await store.CompletePairingAsync(requestId, request.Secret, cancellationToken);
            AppendCookie(context, DisplayCookieName, completion.RawToken, completion.Result.ExpiresAt);
            return Results.Ok(completion.Result);
        }).RequireRateLimiting("pairing-complete");

        app.MapGet("/api/v1/customer-displays/{displayId:guid}/snapshot", async (
            Guid displayId,
            HttpContext context,
            DualScreenStore store,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireDisplayAsync(context, displayId, store, cancellationToken);
            return Results.Ok(await store.GetSnapshotAsync(displayId, principal.TerminalId, cancellationToken));
        }).RequireRateLimiting("display-read");

        app.MapPost("/api/v1/terminals/{terminalId:guid}/display-sessions/revoke", async (
            Guid terminalId,
            HttpContext context,
            DualScreenStore store,
            IAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierPermissionAsync(
                context, terminalId, store, authorization, cancellationToken);
            var count = await store.RevokeDisplaySessionsAsync(terminalId, cancellationToken);
            return Results.Ok(new { revoked = count });
        }).RequireRateLimiting("terminal-write");
    }

    private static async Task<CashierPrincipal> RequireCashierPermissionAsync(
        HttpContext context,
        Guid terminalId,
        DualScreenStore store,
        IAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        var principal = await RequireCashierAsync(context, terminalId, store, cancellationToken);
        await authorization.AuthorizeAsync(
            principal.UserId,
            CashierMutationPermission,
            cancellationToken);
        return principal;
    }

    private static async Task<CashierPrincipal> RequireCashierAsync(
        HttpContext context,
        Guid terminalId,
        DualScreenStore store,
        CancellationToken cancellationToken)
    {
        var cashierToken = context.Request.Cookies[CashierCookieName];
        var principal = await store.AuthenticateCashierAsync(cashierToken, terminalId, cancellationToken);
        if (principal is not null)
            return principal;

        var displayToken = context.Request.Cookies[DisplayCookieName];
        if (await store.AuthenticateDisplayAsync(displayToken, null, cancellationToken) is not null)
            throw new DualScreenForbiddenException("Customer display sessions cannot mutate cashier resources.");
        throw new DualScreenUnauthorizedException("Cashier authentication is required.");
    }

    private static async Task<DisplayPrincipal> RequireDisplayAsync(
        HttpContext context,
        Guid displayId,
        DualScreenStore store,
        CancellationToken cancellationToken)
    {
        var token = context.Request.Cookies[DisplayCookieName];
        var principal = await store.AuthenticateDisplayAsync(token, null, cancellationToken);
        if (principal is null)
            throw new DualScreenUnauthorizedException("Customer display authentication is required.");
        if (principal.DisplayId != displayId)
            throw new DualScreenForbiddenException("Customer display is not authorized for this resource.");
        return principal;
    }

    private static Task NotifyAsync(
        IHubContext<CustomerDisplayHub> hub,
        Guid terminalId,
        Guid orderId,
        long revision,
        CancellationToken cancellationToken)
        => hub.Clients.Group(TerminalGroup(terminalId)).SendAsync(
            CustomerDisplayHub.SnapshotChanged,
            new { orderId, revision, kind = "OrderChanged" },
            cancellationToken);

    private static void AppendCookie(
        HttpContext context,
        string name,
        string value,
        DateTimeOffset expiresAt)
    {
        context.Response.Cookies.Append(name, value, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = expiresAt,
            IsEssential = true,
        });
    }

}
