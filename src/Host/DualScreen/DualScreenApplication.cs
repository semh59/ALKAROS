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
using ALKAROS.Host.Experience.Authorization;
using ALKAROS.Host.Experience.Billing;
using ALKAROS.Host.Experience.Catalog;
using ALKAROS.Host.Experience.KitchenOperations;
using ALKAROS.Host.Experience.Menu;
using ALKAROS.Host.Experience.NfcOrdering;
using ALKAROS.Host.Experience.OfflineReconciliation;
using ALKAROS.Host.Experience.RelaySettings;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Host.Experience.Roles;
using ALKAROS.Host.Experience.Tables;
using ALKAROS.Host.Experience.WaiterNotifications;
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
    public const string CashierCookieName = "alkaros.cashier";
    public const string DisplayCookieName = "alkaros.customer-display";
    public const string KitchenStationEnvironmentVariable = "ALKAROS_KITCHEN_STATION_ID";

    // Absolute origin the reverse proxy serves the customer display from
    // (e.g. https://display.pos.local:8443). Returned by runtime-configuration
    // so the cashier's "customer display" link opens the isolated display
    // origin (finding B-4) instead of a same-origin /display that the api
    // refuses. Unset -> the link stays relative (single-origin / legacy).
    public const string CustomerDisplayOriginUrlEnvironmentVariable = "ALKAROS_CUSTOMER_DISPLAY_ORIGIN_URL";

    public static int Run(string[] args)
    {
        var options = DualScreenOptions.Parse(args);
        var app = Build(options);
        app.Run();
        return 0;
    }

    public static WebApplication Build(DualScreenOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseUrls(options.AllListenUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (options.ServesHttpsDirectly)
        {
            var serverCertificate = DualScreenTls.Resolve(options, builder.Environment.ContentRootPath);
            if (serverCertificate is not null)
            {
                builder.WebHost.ConfigureKestrel(kestrel =>
                    kestrel.ConfigureHttpsDefaults(https => https.ServerCertificate = serverCertificate));
            }
        }
        builder.Services.ConfigureHttpJsonOptions(json =>
            json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        var dataSource = NpgsqlDataSource.Create(options.ConnectionString);
        builder.Services.AddSingleton(dataSource);
        builder.Services.AddSingleton<System.Data.Common.DbDataSource>(dataSource);

        // The bounded-context modules are the single composition root for the
        // serve host: their repository/service registrations come straight from
        // ModuleRegistry.DefaultCatalog instead of being hand-copied here where
        // they could silently drift (deep-analysis finding B-2). Applied before
        // the experience extensions so their TryAdd* calls defer to the module
        // registrations. Only genuinely host-local services are registered by
        // hand below.
        var moduleComposition = ModuleRegistry.ComposeRoot(ModuleRegistry.DefaultCatalog);
        HostComposition.ApplyComposedModuleServices(builder.Services, moduleComposition.Services);

        // Drains the transactional outbox and fans each table event out to the
        // module IIntegrationEventConsumer registrations above (Order, Bill
        // reparent their rows after a table merge/transfer/unmerge).
        builder.Services.AddOutboxDispatch();

        builder.Services.AddSingleton<DualScreenStore>();
        builder.Services.AddSingleton<SubmitOrderHandler>();
        builder.Services.AddTableManagementExperience();
        builder.Services.AddCatalogManagement();
        // V1-RMD-131: found by an independent audit (2026-09-09) — the Menu
        // module (persistent named menus + the daily-specials lifecycle) was
        // fully registered by MenuModule above but had zero HTTP surface;
        // nothing could ever reach it.
        builder.Services.AddMenuManagementExperience();
        builder.Services.AddKitchenOperationsExperience();
        builder.Services.AddOrderManagementExperience();
        builder.Services.AddNfcOrderingExperience();
        builder.Services.AddRelaySettingsExperience();
        builder.Services.AddBillingSplitExperience();
        builder.Services.AddAuthorizationDecisionExperience();
        builder.Services.AddRoleManagementExperience();
        builder.Services.AddOfflineReconciliationExperience();
        builder.Services.AddWaiterNotificationsExperience();
        builder.Services.AddSingleton<IOrderSubmissionDispatcher>(services =>
        {
            var stationId = Environment.GetEnvironmentVariable(KitchenStationEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(stationId))
            {
                throw new InvalidOperationException(
                    $"{KitchenStationEnvironmentVariable} is required before cashier order submission is enabled.");
            }

            // The env var is now the fallback station for items that no printer
            // route resolves; configured routes drive per-item station
            // assignment and produce one ticket per station
            // (deep-analysis finding B-3).
            return new KitchenOrderSubmissionDispatcher(
                services.GetRequiredService<IKitchenTicketRepository>(),
                stationId,
                services.GetRequiredService<IKitchenPrinterRouter>(),
                services.GetRequiredService<IPrinterRepository>(),
                services.GetRequiredService<IPrinterRouteRepository>());
        });
        builder.Services.AddSignalR(options => options.EnableDetailedErrors = false);
        builder.Services.Configure<ForwardedHeadersOptions>(forwarded =>
        {
            forwarded.ForwardedHeaders = options.TrustedProxies is { Count: > 0 }
                || options.TrustedNetworks is { Count: > 0 }
                    ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                    : ForwardedHeaders.None;
            forwarded.ForwardLimit = 1;
            forwarded.RequireHeaderSymmetry = true;
            forwarded.KnownNetworks.Clear();
            forwarded.KnownProxies.Clear();
            if (options.TrustedProxies is not null)
            {
                foreach (var proxy in options.TrustedProxies)
                    forwarded.KnownProxies.Add(proxy);
            }
            if (options.TrustedNetworks is not null)
            {
                foreach (var network in options.TrustedNetworks)
                    forwarded.KnownNetworks.Add(network);
            }
        });
        builder.Services.AddRateLimiter(rateLimiter =>
        {
            rateLimiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            rateLimiter.OnRejected = async (rejected, cancellationToken) =>
            {
                rejected.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                    rejected.HttpContext.Response.Headers.RetryAfter =
                        seconds.ToString(CultureInfo.InvariantCulture);
                }
                await rejected.HttpContext.Response.WriteAsJsonAsync(
                    new ApiErrorEnvelope(new ApiError(
                        "RATE_LIMITED",
                        "Çok fazla deneme yapıldı. Lütfen kısa süre sonra yeniden deneyin.",
                        StatusCodes.Status429TooManyRequests,
                        rejected.HttpContext.TraceIdentifier)),
                    cancellationToken);
            };
            rateLimiter.AddPolicy("login", context =>
                FixedWindow(ClientPartition(context), 10));
            rateLimiter.AddPolicy("pairing-create", context =>
                FixedWindow($"{ClientPartition(context)}:pairing-create", 10));
            rateLimiter.AddPolicy("pairing-approve", context =>
                FixedWindow(RoutePartition(context, "terminalId", "pairing-approve"), 10));
            rateLimiter.AddPolicy("pairing-complete", context =>
                FixedWindow(RoutePartition(context, "requestId", "pairing-complete"), 60));
            rateLimiter.AddPolicy("terminal-read", context =>
                FixedWindow(RoutePartition(context, "terminalId", "terminal-read"), 240));
            rateLimiter.AddPolicy("terminal-write", context =>
                FixedWindow(RoutePartition(context, "terminalId", "terminal-write"), 120));
            // V12-NFC-001: unauthenticated customer-facing surface — unlike
            // every other write policy above (partitioned by an
            // authenticated terminal/display id), this partitions by table
            // id since there is no session to key on. A generous-but-bounded
            // limit: several guests at one table placing a few rounds each.
            rateLimiter.AddPolicy("nfc-order", context =>
                FixedWindow(RoutePartition(context, "tableId", "nfc-order"), 30));
            rateLimiter.AddPolicy("display-read", context =>
                FixedWindow(RoutePartition(context, "displayId", "display-read"), 240));
        });

        var app = builder.Build();
        app.UseForwardedHeaders();
        app.Use(async (context, next) =>
        {
            var insecureDevelopmentLoopback = app.Environment.IsDevelopment()
                && options.AllowInsecureLoopbackDevelopment
                && IPAddress.IsLoopback(context.Connection.LocalIpAddress ?? IPAddress.None)
                && IPAddress.IsLoopback(context.Connection.RemoteIpAddress ?? IPAddress.None);
            var loopbackReadinessProbe = context.Request.Path.Equals("/health/ready", StringComparison.Ordinal)
                && IPAddress.IsLoopback(context.Connection.LocalIpAddress ?? IPAddress.None)
                && IPAddress.IsLoopback(context.Connection.RemoteIpAddress ?? IPAddress.None);
            if (!context.Request.IsHttps && !insecureDevelopmentLoopback && !loopbackReadinessProbe)
            {
                await Error(
                    context,
                    StatusCodes.Status400BadRequest,
                    "HTTPS_REQUIRED",
                    "Bu adres güvenli HTTPS bağlantısı gerektirir.").ExecuteAsync(context);
                return;
            }
            await next();
        });

        // Customer-display origin isolation (deep-analysis finding B-4). The
        // display origin is a distinct browser origin from the cashier so their
        // localStorage - and the cashier terminal id / bill id - do not leak
        // across. It exposes only the display API, the display hub and the
        // static client shell; the main origin refuses the display-only routes.
        // Two ways to recognise the display origin:
        //   * a dedicated listen port (--customer-display-urls), used when the
        //     host serves the static bundles itself; or
        //   * a header the trusted reverse proxy sets on the display virtual
        //     host (--customer-display-origin-header), used in --api-only mode
        //     where the proxy owns the origin split.
        // With neither configured this middleware is not added and a single
        // origin serves everything as before.
        var customerDisplayPorts = options.CustomerDisplayPorts.ToHashSet();
        var displayOriginHeader = options.CustomerDisplayOriginHeader;
        if (customerDisplayPorts.Count > 0 || displayOriginHeader is not null)
        {
            app.Use(async (context, next) =>
            {
                // The header is only trustworthy from the reverse proxy. Reaching
                // this middleware with context.Request.IsHttps set means the
                // forwarded-headers middleware honoured X-Forwarded-Proto, which
                // it only does for a --trusted-proxy / --trusted-network peer -
                // the same peer that sets the origin header. A direct client is
                // plain HTTP here and was already stopped by the HTTPS gate, so
                // it can neither reach this code nor forge the display origin.
                var onDisplayOrigin = customerDisplayPorts.Contains(context.Connection.LocalPort)
                    || (displayOriginHeader is not null
                        && context.Request.IsHttps
                        && string.Equals(
                            context.Request.Headers[displayOriginHeader],
                            "display",
                            StringComparison.Ordinal));
                var path = context.Request.Path;
                var isApi = path.StartsWithSegments("/api", StringComparison.Ordinal);
                var isDisplayApi = path.StartsWithSegments("/api/v1/customer-displays", StringComparison.Ordinal);
                var isDisplayHub = path.StartsWithSegments(CustomerDisplayHub.Route, StringComparison.Ordinal);

                if (onDisplayOrigin && isApi && !isDisplayApi)
                {
                    await Error(context, StatusCodes.Status404NotFound, "NOT_FOUND",
                        "Bu adres müşteri ekranı origin'inde sunulmuyor.").ExecuteAsync(context);
                    return;
                }

                if (!onDisplayOrigin && (isDisplayApi || isDisplayHub))
                {
                    await Error(context, StatusCodes.Status404NotFound, "NOT_FOUND",
                        "Müşteri ekranı adresleri yalnızca ayrı origin'den sunulur.").ExecuteAsync(context);
                    return;
                }

                await next();
            });
        }

        app.UseRouting();
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
        UseDualScreenErrorHandling(app);

        MapApi(app);
        app.MapCatalogManagement();
        app.MapMenuManagement();
        app.MapTableManagementApi();
        app.MapKitchenOperationsApi();
        app.MapOrderManagementApi();
        app.MapNfcOrderingApi();
        app.MapRelaySettingsApi();
        app.MapBillingSplitApi();
        app.MapAuthorizationDecisionApi();
        app.MapRoleManagementApi();
        app.MapOfflineReconciliationApi();
        app.MapWaiterNotificationsApi();
        app.MapHub<CustomerDisplayHub>(CustomerDisplayHub.Route);
        app.MapMethods(
            "/api/{**path}",
            ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"],
            (HttpContext context) => Error(
                context,
                StatusCodes.Status404NotFound,
                "NOT_FOUND",
                "İstenen API adresi bulunamadı."));

        if (options.ApiOnly)
        {
            // The reverse proxy serves the PosTerminal / WaiterPwa / Cashier
            // bundles and owns the SPA fallback; anything not matched by an API
            // route or hub above is genuinely not found here.
            app.MapFallback((HttpContext context) => Error(
                context,
                StatusCodes.Status404NotFound,
                "NOT_FOUND",
                "İstenen adres bu API sunucusunda bulunmuyor."));
        }
        else
        {
            var fileProvider = new PhysicalFileProvider(options.WebRoot);
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
            app.MapFallback(async context =>
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.SendFileAsync(Path.Combine(options.WebRoot, "index.html"));
            });
        }
        return app;
    }

    public static string TerminalGroup(Guid terminalId) => $"terminal:{terminalId:D}";

    public static IApplicationBuilder UseDualScreenErrorHandling(IApplicationBuilder application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return application.Use(async (context, next) =>
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
    }

    private static RateLimitPartition<string> FixedWindow(string partitionKey, int permitLimit)
        => RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        });

    private static string ClientPartition(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string RoutePartition(HttpContext context, string routeName, string operation)
    {
        var routeValue = context.Request.RouteValues.TryGetValue(routeName, out var value)
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;
        return $"{ClientPartition(context)}:{operation}:{routeValue ?? "missing"}";
    }

    private static readonly Action<ILogger, string, string, Exception?> LogServerError =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5000, nameof(LogServerError)),
            "Unhandled error on {Path} (TraceIdentifier: {TraceIdentifier})");

    private static IResult Error(HttpContext context, int status, string code, string message)
        => Results.Json(
            new ApiErrorEnvelope(new ApiError(code, message, status, context.TraceIdentifier)),
            statusCode: status);

    private static async Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        var (status, code, message) = exception switch
        {
            DualScreenUnauthorizedException => (401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
            DualScreenForbiddenException => (403, "FORBIDDEN", "Bu işlem için yetkiniz yok."),
            AuthorizationDeniedException => (403, "FORBIDDEN", "Bu işlem için yetkiniz yok."),
            DualScreenNotFoundException => (404, "NOT_FOUND", "İstenen kayıt bulunamadı."),
            DualScreenConflictException => (409, "CONCURRENT_MODIFICATION", "Kayıt başka bir işlem tarafından değiştirildi."),
            SubmitOrderIdempotencyConflictException => (409, "IDEMPOTENCY_CONFLICT", "İşlem anahtarı farklı bir istekle kullanılmış."),
            StaleOrderVersionException => (409, "CONCURRENT_MODIFICATION", "Sipariş başka bir işlem tarafından değiştirildi."),
            OrderNotFoundException => (404, "ORDER_NOT_FOUND", "Sipariş bulunamadı."),
            ArgumentException or BadHttpRequestException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            PostgresException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
            _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
        };

        if (status >= 500)
        {
            var logger = context.RequestServices.GetService<ILogger<DualScreenOptions>>();
            if (logger is not null)
            {
                LogServerError(logger, context.Request.Path, context.TraceIdentifier, exception);
            }
        }

        if (context.Response.HasStarted)
        {
            context.Abort();
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(
            new ApiErrorEnvelope(new ApiError(code, message, status, context.TraceIdentifier)),
            cancellationToken: context.RequestAborted);
    }
}
