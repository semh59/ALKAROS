using System.Threading.RateLimiting;
using ALKAROS.Identity.Authentication;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace ALKAROS.Host.DualScreen;

public static class DualScreenApplication
{
    public const string CashierCookieName = "alkaros.cashier";
    public const string DisplayCookieName = "alkaros.customer-display";

    public static int Run(string[] args)
    {
        var options = DualScreenOptions.Parse(args);
        var app = Build(options);
        app.Run(options.Url);
        return 0;
    }

    public static WebApplication Build(DualScreenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls(options.Url);
        builder.Services.AddSingleton(NpgsqlDataSource.Create(options.ConnectionString));
        builder.Services.AddSingleton<DualScreenStore>();
        builder.Services.AddSingleton<IUserStore, PostgresUserStore>();
        builder.Services.AddSingleton<AuthenticationService>();
        builder.Services.AddSingleton<IDeviceSessionRepository, PostgresDeviceSessionRepository>();
        builder.Services.AddSingleton<IDeviceSessionService, DeviceSessionService>();
        builder.Services.AddSingleton<IOrderRepository, PostgresOrderRepository>();
        builder.Services.AddSingleton<SubmitOrderHandler>();
        builder.Services.AddSignalR(options => options.EnableDetailedErrors = false);
        builder.Services.AddRateLimiter(rateLimiter =>
        {
            rateLimiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            rateLimiter.OnRejected = async (rejected, cancellationToken) =>
            {
                rejected.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await rejected.HttpContext.Response.WriteAsJsonAsync(
                    new ApiErrorEnvelope(new ApiError(
                        "RATE_LIMITED",
                        "Çok fazla deneme yapıldı. Lütfen kısa süre sonra yeniden deneyin.",
                        StatusCodes.Status429TooManyRequests,
                        rejected.HttpContext.TraceIdentifier)),
                    cancellationToken);
            };
            rateLimiter.AddFixedWindowLimiter("login", limiter =>
            {
                limiter.PermitLimit = 10;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
                limiter.AutoReplenishment = true;
            });
            rateLimiter.AddFixedWindowLimiter("pairing-create", limiter =>
            {
                limiter.PermitLimit = 10;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
                limiter.AutoReplenishment = true;
            });
            rateLimiter.AddFixedWindowLimiter("pairing-approve", limiter =>
            {
                limiter.PermitLimit = 10;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
                limiter.AutoReplenishment = true;
            });
            rateLimiter.AddFixedWindowLimiter("pairing-complete", limiter =>
            {
                limiter.PermitLimit = 60;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
                limiter.AutoReplenishment = true;
            });
        });

        var app = builder.Build();
        app.UseRateLimiter();
        app.Use(async (context, next) =>
        {
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; " +
                "connect-src 'self' ws: wss:; object-src 'none'; base-uri 'none'; frame-ancestors 'none'";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Cache-Control"] = context.Request.Path.StartsWithSegments("/api")
                ? "no-store"
                : "no-cache";
            await next();
        });
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception exception)
            {
                await WriteErrorAsync(context, exception);
            }
        });

        MapApi(app);
        app.MapHub<CustomerDisplayHub>(CustomerDisplayHub.Route);
        app.MapMethods(
            "/api/{**path}",
            ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"],
            (HttpContext context) => Error(
                context,
                StatusCodes.Status404NotFound,
                "NOT_FOUND",
                "İstenen API adresi bulunamadı."));

        var fileProvider = new PhysicalFileProvider(options.WebRoot);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
        app.MapFallback(async context =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(Path.Combine(options.WebRoot, "index.html"));
        });
        return app;
    }

    public static string TerminalGroup(Guid terminalId) => $"terminal:{terminalId:D}";

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
            return Results.Ok(new LoginResponse(success.UserId, success.DisplayName, request.TerminalId));
        }).RequireRateLimiting("login");

        app.MapGet("/api/v1/auth/session", async (
            Guid terminalId,
            HttpContext context,
            DualScreenStore store,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierAsync(context, terminalId, store, cancellationToken);
            return Results.Ok(new LoginResponse(principal.UserId, principal.DisplayName, terminalId));
        });

        app.MapPost("/api/v1/auth/logout", async (
            Guid terminalId,
            HttpContext context,
            IDeviceSessionService sessions,
            DualScreenStore store,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierAsync(context, terminalId, store, cancellationToken);
            await sessions.RevokeAsync(principal.SessionId, cancellationToken);
            context.Response.Cookies.Delete(CashierCookieName);
            return Results.NoContent();
        });

        app.MapGet("/api/v1/terminals/{terminalId:guid}/catalog", async (
            Guid terminalId,
            HttpContext context,
            DualScreenStore store,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierAsync(context, terminalId, store, cancellationToken);
            return Results.Ok(await store.GetCatalogAsync(cancellationToken));
        });

        app.MapPost("/api/v1/terminals/{terminalId:guid}/orders", async (
            Guid terminalId,
            HttpContext context,
            DualScreenStore store,
            IHubContext<CustomerDisplayHub> hub,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var created = await store.StartOrderAsync(terminalId, cancellationToken);
            await NotifyAsync(hub, terminalId, created.OrderId, created.Revision, cancellationToken);
            return Results.Created($"/api/v1/terminals/{terminalId:D}/orders/{created.OrderId:D}", created);
        });

        app.MapGet("/api/v1/terminals/{terminalId:guid}/orders/active", async (
            Guid terminalId,
            HttpContext context,
            DualScreenStore store,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var displayId = await store.GetActiveDisplayIdAsync(terminalId, cancellationToken) ?? Guid.Empty;
            return Results.Ok(await store.GetSnapshotAsync(displayId, terminalId, cancellationToken));
        });

        app.MapPost("/api/v1/terminals/{terminalId:guid}/orders/{orderId:guid}/items", async (
            Guid terminalId,
            Guid orderId,
            AddOrderItemRequest request,
            HttpContext context,
            DualScreenStore store,
            IHubContext<CustomerDisplayHub> hub,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var mutation = await store.AddItemAsync(terminalId, orderId, request, cancellationToken);
            await NotifyAsync(hub, terminalId, orderId, mutation.Revision, cancellationToken);
            return Results.Ok(mutation);
        });

        app.MapPatch("/api/v1/terminals/{terminalId:guid}/orders/{orderId:guid}/items/{itemId:guid}", async (
            Guid terminalId,
            Guid orderId,
            Guid itemId,
            ChangeOrderItemQuantityRequest request,
            HttpContext context,
            DualScreenStore store,
            IHubContext<CustomerDisplayHub> hub,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var mutation = await store.ChangeItemQuantityAsync(
                terminalId, orderId, itemId, request, cancellationToken);
            await NotifyAsync(hub, terminalId, orderId, mutation.Revision, cancellationToken);
            return Results.Ok(mutation);
        });

        app.MapDelete("/api/v1/terminals/{terminalId:guid}/orders/{orderId:guid}/items/{itemId:guid}", async (
            Guid terminalId,
            Guid orderId,
            Guid itemId,
            long expectedRevision,
            HttpContext context,
            DualScreenStore store,
            IHubContext<CustomerDisplayHub> hub,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var mutation = await store.RemoveItemAsync(
                terminalId, orderId, itemId, expectedRevision, cancellationToken);
            await NotifyAsync(hub, terminalId, orderId, mutation.Revision, cancellationToken);
            return Results.Ok(mutation);
        });

        app.MapPost("/api/v1/terminals/{terminalId:guid}/orders/{orderId:guid}/submit", async (
            Guid terminalId,
            Guid orderId,
            SubmitOrderRequest request,
            HttpContext context,
            DualScreenStore store,
            SubmitOrderHandler handler,
            IHubContext<CustomerDisplayHub> hub,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var result = await handler.HandleAsync(new SubmitOrderCommand(
                $"cashier:{terminalId:D}", request.OperationId, orderId, request.ExpectedRevision,
                principal.UserId, DateTimeOffset.UtcNow, "Cashier submitted order"), cancellationToken);
            await NotifyAsync(hub, terminalId, orderId, result.RowVersion, cancellationToken);
            return Results.Ok(result);
        });

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
            CancellationToken cancellationToken) =>
        {
            await RequireCashierAsync(context, terminalId, store, cancellationToken);
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
        });

        app.MapPost("/api/v1/terminals/{terminalId:guid}/display-sessions/revoke", async (
            Guid terminalId,
            HttpContext context,
            DualScreenStore store,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierAsync(context, terminalId, store, cancellationToken);
            var count = await store.RevokeDisplaySessionsAsync(terminalId, cancellationToken);
            return Results.Ok(new { revoked = count });
        });
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
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = expiresAt,
            IsEssential = true,
        });
    }

    private static IResult Error(HttpContext context, int status, string code, string message)
        => Results.Json(
            new ApiErrorEnvelope(new ApiError(code, message, status, context.TraceIdentifier)),
            statusCode: status);

    private static async Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            context.Abort();
            return;
        }

        var (status, code, message) = exception switch
        {
            DualScreenUnauthorizedException => (401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            DualScreenForbiddenException => (403, "FORBIDDEN", "Bu işlem için yetkiniz yok."),
            DualScreenNotFoundException => (404, "NOT_FOUND", "İstenen kayıt bulunamadı."),
            DualScreenConflictException => (409, "CONCURRENT_MODIFICATION", "Kayıt başka bir işlem tarafından değiştirildi."),
            SubmitOrderIdempotencyConflictException => (409, "IDEMPOTENCY_CONFLICT", "İşlem anahtarı farklı bir istekle kullanılmış."),
            StaleOrderVersionException => (409, "CONCURRENT_MODIFICATION", "Sipariş başka bir işlem tarafından değiştirildi."),
            OrderNotFoundException => (404, "ORDER_NOT_FOUND", "Sipariş bulunamadı."),
            ArgumentException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            PostgresException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
        };
        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(
            new ApiErrorEnvelope(new ApiError(code, message, status, context.TraceIdentifier)),
            cancellationToken: context.RequestAborted);
    }
}
