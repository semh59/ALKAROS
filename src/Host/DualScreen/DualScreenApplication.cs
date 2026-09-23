using System.Net;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ALKAROS.Cash.Contracts;
using ALKAROS.Cash.TenderHandler;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Identity.Authentication;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Kitchen.Routing;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using ALKAROS.Host.Composition;
using ALKAROS.Host.Composition.Modules;
using ALKAROS.Host.Experience.Authorization;
using ALKAROS.Host.Experience.Billing;
using ALKAROS.Host.Experience.Catalog;
using ALKAROS.Host.Experience.Inventory;
using ALKAROS.Host.Experience.KitchenOperations;
using ALKAROS.Host.Experience.Menu;
using ALKAROS.Host.Experience.NfcOrdering;
using ALKAROS.Host.Experience.Production;
using ALKAROS.Host.Experience.Purchasing;
using ALKAROS.Host.Experience.Settings;
using ALKAROS.Host.Experience.Recipes;
using ALKAROS.Host.Experience.InventoryReporting;
using ALKAROS.Host.Experience.Reporting;
using ALKAROS.Host.Experience.Reconciliation;
using ALKAROS.Host.Experience.Observability;
using ALKAROS.Host.Experience.OfflineReconciliation;
using ALKAROS.Host.Experience.QrOrdering;
using ALKAROS.Host.Experience.RelaySettings;
using ALKAROS.Host.Experience.TokenTerminalSettings;
using ALKAROS.Host.Experience.QnbCredentialSettings;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Host.Experience.Orders.OrderStockConsumption;
using ALKAROS.Host.Experience.Orders.SubmissionStockConsumption;
using ALKAROS.Host.Experience.Roles;
using ALKAROS.Host.Experience.Tables;
using ALKAROS.Host.Experience.HelpRequests;
using ALKAROS.Host.Experience.WaiterNotifications;
using ALKAROS.Host.Experience.WebPush;
using ALKAROS.Host.Outbox;
using ALKAROS.QrOrdering.RelaySecurity;
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
        // V1-RMD-132/V1-RMD-133: same rationale — Purchasing (suppliers,
        // purchase orders, goods receipt) and Production (batch lifecycle +
        // stock effects) were both fully registered by their own modules
        // above but had zero HTTP surface.
        builder.Services.AddPurchasingManagementExperience();
        builder.Services.AddProductionManagementExperience();
        // V1-RMD-246: ISettingsService.SetValueAsync/DeactivateAsync existed
        // since V1-SET-001 with zero HTTP surface — a setting could only
        // ever change via direct database access.
        builder.Services.AddSettingsManagementExperience();
        // V1-SET-008: the business's own QR-page logo — same manager gate
        // V1-RMD-246 just built for business.name/business.accent_theme.
        builder.Services.AddBusinessIdentityLogoExperience();
        // V1-RMD-143: Semih's decision (2026-09-09) that order acceptance
        // should really decrement stock needed this first — nothing could
        // ever configure which product maps to which stock item before now.
        builder.Services.AddStockMasterExperience();
        // V11-RCP-003: which recipe a catalog product corresponds to, the
        // first step towards a real actual-vs-theoretical variance report.
        builder.Services.AddRecipeCatalogMappingExperience();
        // V1-RMD-247: IRecipeCostSnapshotService (V11-RCP-002) existed with
        // zero HTTP surface — a recipe's cost could never be calculated
        // through the running application.
        builder.Services.AddRecipeCostSnapshotExperience();
        // V11-RPT-002: the critical-stock report finally gets a Host
        // endpoint, plus a live low-stock alert broadcast.
        builder.Services.AddInventoryReportingExperience();
        // V1-RMD-249: IOperationalReportService (V1-RPT-001, EOD business-day
        // open/close) existed with zero HTTP surface.
        builder.Services.AddEndOfDayExperience();
        // V1-RMD-250: IReconciliationService (V1-REC-001, discrepancy case
        // lifecycle) existed with zero HTTP surface.
        builder.Services.AddReconciliationCaseExperience();
        // V1-RMD-251: IAlertService (V1-ALT-001) and
        // IObservabilityService's health-check surface (V1-OBS-001)
        // existed with zero HTTP surface.
        builder.Services.AddObservabilityExperience();
        builder.Services.AddKitchenOperationsExperience();
        builder.Services.AddOrderManagementExperience();
        builder.Services.AddNfcOrderingExperience();
        // V12-CWB-001: the public-relay counterpart to NFC's own trusted-LAN
        // customer ordering surface — see QrOrderingEndpoints.cs's own doc
        // comment for why it needs a session-exchange step NFC does not.
        builder.Services.AddQrOrderingExperience();
        builder.Services.AddRelaySettingsExperience();
        builder.Services.AddTokenTerminalSettingsExperience();
        builder.Services.AddQnbCredentialSettingsExperience();
        builder.Services.AddBillingSplitExperience();
        builder.Services.AddAuthorizationDecisionExperience();
        builder.Services.AddRoleManagementExperience();
        builder.Services.AddOfflineReconciliationExperience();
        builder.Services.AddWaiterNotificationsExperience();
        builder.Services.AddWebPushExperience();
        builder.Services.AddHelpRequestExperience();
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
            var kitchen = new KitchenOrderSubmissionDispatcher(
                services.GetRequiredService<IKitchenTicketRepository>(),
                stationId,
                services.GetRequiredService<IKitchenPrinterRouter>(),
                services.GetRequiredService<IPrinterRepository>(),
                services.GetRequiredService<IPrinterRouteRepository>());

            // V1-RMD-144: a Cashier/Waiter order consumes its stock the moment
            // it is sent to the kitchen (QR/NFC still consume on Accept). Stock
            // runs first so an order stock cannot cover throws before any
            // kitchen ticket row exists, and the whole submission rolls back.
            return new CompositeOrderSubmissionDispatcher(
                new OrderSubmissionStockDispatcher(services.GetRequiredService<OrderStockConsumptionService>()),
                kitchen);
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
                FixedWindow(ClientPartition(context), LoginRateLimitPermits()));
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
            // V12-CWB-001: relay-facing, so unlike every other unauthenticated
            // policy above there is no table id to partition on yet at this
            // route (a session does not exist until it succeeds) — keyed by
            // source IP alone, per RelayAbusePolicy.PerIpRequestLimit
            // (V12-QRS-002's own declared bound for exactly this endpoint).
            rateLimiter.AddPolicy("qr-session", context =>
                FixedWindow(ClientPartition(context), RelayAbusePolicy.PerIpRequestLimit));
            // Every QR customer action after session issuance shares this
            // cap, partitioned by the customer's own session token
            // (RelayAbusePolicy.PerTokenRequestLimit) rather than by IP —
            // several customers at one table share a NAT'd/mobile IP, but
            // each holds a distinct session.
            rateLimiter.AddPolicy("qr-order", context =>
                FixedWindow(HeaderPartition(context, QrOrderingEndpoints.SessionHeaderName, "qr-order"), RelayAbusePolicy.PerTokenRequestLimit));
            // V1-SET-007/008: /branding and /logo are deliberately session-free
            // (the page's first render happens before a session exists), so
            // HeaderPartition would collapse every visitor without a session
            // header into one shared "qr-order:missing" bucket alongside
            // qr-order's real per-session traffic — starving unrelated
            // customers' menu/order calls once enough branding/logo reads
            // land in the same window. Partitioned by IP instead, same bound
            // as qr-session.
            rateLimiter.AddPolicy("qr-public", context =>
                FixedWindow(ClientPartition(context), RelayAbusePolicy.PerIpRequestLimit));
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
            // V12-CWB-001: found while first exercising the real loopback
            // relay path end-to-end (V1-RMD-140 shipped with only container-
            // topology reasoning + a test that trusted loopback as a
            // forwarded-header proxy — compose.yaml never does that, since
            // --trusted-network only covers the Docker bridge range). Without
            // this, every single NFC/QR request the Cloudflare Tunnel
            // connector forwards was rejected here with HTTPS_REQUIRED before
            // the origin gate below even ran — the whole relay path was
            // silently non-functional. Cloudflare's own edge already
            // terminated real TLS from the customer's browser; the
            // connector's hop to this process is the same kind of internal,
            // unencrypted last leg Caddy's own trusted-proxy forwarding
            // already represents for the LAN path.
            //
            // V12-QRT-005: the connector no longer shares this process's
            // loopback (its own container now) — NfcLoopbackOriginTrusted
            // still covers a genuine loopback caller (e.g. local debugging),
            // and NfcTrustedNetworks covers the connector's real production
            // path: a source address inside the small, dedicated
            // `relay-internal` Docker network (compose.yaml) joined only by
            // `api` and `connector`. Same unspoofable shape as the loopback
            // check it extends: nothing outside that specific two-container
            // network can ever present a source address inside it, same as
            // nothing outside the container could forge loopback before.
            var remoteAddress = context.Connection.RemoteIpAddress;
            var nfcRelayRequest =
                (options.NfcLoopbackOriginTrusted
                    && IPAddress.IsLoopback(context.Connection.LocalIpAddress ?? IPAddress.None)
                    && IPAddress.IsLoopback(remoteAddress ?? IPAddress.None))
                || (remoteAddress is not null
                    && options.NfcTrustedNetworks is { Count: > 0 } nfcTrustedNetworks
                    && nfcTrustedNetworks.Any(network => network.Contains(remoteAddress)));
            if (!context.Request.IsHttps && !insecureDevelopmentLoopback && !loopbackReadinessProbe && !nfcRelayRequest)
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

        // V1-RMD-139: found by an independent audit (2026-09-09) — the NFC
        // customer ordering page shared the exact same origin as the
        // cashier/admin bundle, so an anonymous customer's phone could reach
        // every non-NFC API too (server-side authorization already refused
        // those calls; this closes the origin itself, defense in depth).
        // Three recognition mechanisms — a dedicated port (--nfc-urls), a
        // trusted-proxy header (--nfc-origin-header), or a loopback source
        // (--nfc-loopback-origin, relay scope hardening 2026-09-09: the
        // co-located Cloudflare Tunnel connector reaches this process over
        // loopback — see DualScreenOptions.NfcLoopbackOriginTrusted's own
        // doc comment for why that is a safe signal in compose.yaml's
        // topology) — and not added at all when none is configured.
        // V12-CWB-001: QR's own endpoints (`/api/v1/qr/*`) now exist and are
        // just as customer-facing as NFC's, so this same gate covers both —
        // V1-RMD-140's own "Out of scope" flagged this exact one-line
        // extension as the follow-up once QR's HTTP surface shipped. The
        // CLI flag names stay NFC-prefixed (they predate QR and nothing
        // depends on renaming them); only the route allowlist grows.
        var nfcOriginPorts = options.NfcOriginPorts.ToHashSet();
        var nfcOriginHeader = options.NfcOriginHeader;
        var nfcLoopbackOriginTrusted = options.NfcLoopbackOriginTrusted;
        if (nfcOriginPorts.Count > 0 || nfcOriginHeader is not null || nfcLoopbackOriginTrusted)
        {
            app.Use(async (context, next) =>
            {
                var onNfcOrigin = nfcOriginPorts.Contains(context.Connection.LocalPort)
                    || (nfcOriginHeader is not null
                        && context.Request.IsHttps
                        && string.Equals(
                            context.Request.Headers[nfcOriginHeader],
                            "nfc",
                            StringComparison.Ordinal))
                    || (nfcLoopbackOriginTrusted
                        && context.Connection.RemoteIpAddress is { } remoteAddress
                        && IPAddress.IsLoopback(remoteAddress));
                var path = context.Request.Path;
                var isApi = path.StartsWithSegments("/api", StringComparison.Ordinal);
                var isNfcApi = path.StartsWithSegments("/api/v1/nfc", StringComparison.Ordinal)
                    || path.StartsWithSegments("/api/v1/qr", StringComparison.Ordinal);

                if (onNfcOrigin && isApi && !isNfcApi)
                {
                    await Error(context, StatusCodes.Status404NotFound, "NOT_FOUND",
                        "Bu adres NFC/QR origin'inde sunulmuyor.").ExecuteAsync(context);
                    return;
                }

                if (!onNfcOrigin && isNfcApi)
                {
                    await Error(context, StatusCodes.Status404NotFound, "NOT_FOUND",
                        "NFC/QR sipariş adresleri yalnızca ayrı origin'den sunulur.").ExecuteAsync(context);
                    return;
                }

                await next();
            });
        }

        // V12-CWB-001: RelayAbusePolicy.MaxPayloadBytes bounds a relay-facing
        // QR request's body before it is ever parsed — the relay's public
        // internet exposure has no other body-size backstop the way the
        // LAN-only NFC/cashier surfaces implicitly have. Checked against the
        // declared Content-Length only (Kestrel's own default request-body
        // ceiling remains the backstop for a client that omits or lies about
        // it); QR's own per-item/per-line bounds (QrPendingOrderStore) are
        // the primary defense for order submission itself.
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/v1/qr", StringComparison.Ordinal)
                && context.Request.ContentLength is { } contentLength
                && contentLength > RelayAbusePolicy.MaxPayloadBytes)
            {
                await Error(context, StatusCodes.Status413PayloadTooLarge, "PAYLOAD_TOO_LARGE",
                    "İstek çok büyük.").ExecuteAsync(context);
                return;
            }

            await next();
        });

        // V12-CWB-001: the Cloudflare Tunnel connector reaches this process
        // directly over loopback and never goes through the reverse proxy
        // that normally serves every other static bundle (see
        // DualScreenOptions.QrWebRoot's own doc comment) — this is the one
        // static-file exception to "api-only serves nothing but JSON/hubs",
        // scoped to exactly the QR customer page's own small bundle.
        // Registered before UseRouting so it serves a match directly rather
        // than depending on falling through the endpoint-selection order
        // below (this content needs no origin gating either way — it is
        // public HTML/CSS/JS with no secrets in it, not an API boundary).
        if (options.QrWebRoot is { } qrWebRoot)
        {
            var qrFileProvider = new PhysicalFileProvider(qrWebRoot);
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = qrFileProvider, RequestPath = "/qr" });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = qrFileProvider, RequestPath = "/qr" });
        }

        // V1-RMD-141: the symmetric gap for NFC (see DualScreenOptions.
        // NfcWebRoot's own doc comment) — unlike QrWebRoot's fixed-name
        // paths (/qr, /qr/menu-app.js, …), the NFC page's own URL carries a
        // variable {tableId} segment (/nfc/{tableId}), which UseStaticFiles/
        // UseDefaultFiles alone cannot resolve to index.html (there is no
        // literal file by that name). The fallback below serves it for any
        // GET under /nfc that UseStaticFiles did not already resolve to a
        // real asset file — the same SPA-shell behaviour the non-api-only
        // branch's own MapFallback gives the main bundle, just positioned
        // ahead of routing like QrWebRoot's own registration above.
        if (options.NfcWebRoot is { } nfcWebRoot)
        {
            var nfcFileProvider = new PhysicalFileProvider(nfcWebRoot);
            app.UseStaticFiles(new StaticFileOptions { FileProvider = nfcFileProvider, RequestPath = "/nfc" });
            app.Use(async (context, next) =>
            {
                if (HttpMethods.IsGet(context.Request.Method)
                    && context.Request.Path.StartsWithSegments("/nfc", StringComparison.Ordinal))
                {
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.SendFileAsync(Path.Combine(nfcWebRoot, "index.html"));
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
        app.MapPurchasingManagement();
        app.MapProductionManagement();
        app.MapSettingsManagement();
        app.MapBusinessIdentityLogoApi();
        app.MapStockMasterApi();
        app.MapRecipeCatalogMappingApi();
        app.MapRecipeCostSnapshotApi();
        app.MapInventoryReportingApi();
        app.MapEndOfDayApi();
        app.MapReconciliationCaseApi();
        app.MapObservabilityApi();
        app.MapTableManagementApi();
        app.MapKitchenOperationsApi();
        app.MapOrderManagementApi();
        app.MapNfcOrderingApi();
        app.MapQrOrderingApi();
        app.MapRelaySettingsApi();
        app.MapTokenTerminalSettingsApi();
        app.MapQnbCredentialSettingsApi();
        app.MapBillingSplitApi();
        app.MapAuthorizationDecisionApi();
        app.MapRoleManagementApi();
        app.MapOfflineReconciliationApi();
        app.MapWaiterNotificationsApi();
        app.MapCustomerDisplayScreensaverApi();
        app.MapWebPushApi();
        app.MapHelpRequestApi();
        app.MapCashSessionApi();
        app.MapPaymentTenderApi();
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
            // route, hub or the QR static files (registered earlier, before
            // routing) is genuinely not found here.
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

    // Root-caused while chasing a WaiterPwa E2E flake (2026-09-12): the
    // "login" policy partitions purely by remote IP (ClientPartition), 10
    // requests per rolling minute. Every Playwright test in a single suite
    // run connects from the SAME loopback IP, so the whole ~60-second run
    // shares ONE bucket - specs 01-04's own logins had already spent most
    // of the window's 10 permits by the time 05's load test fired 6 MORE
    // concurrent logins, tipping 2 of them into a real 429 (confirmed via
    // E2E_HOST_LOG, not guessed: "auth/login - 429" in the Host's own
    // access log at the exact failure). This is real production-appropriate
    // behaviour (anti-brute-force) that the E2E suite happened to trip over
    // by generating more traffic from one IP than any single real client
    // legitimately would in a minute - so the fix is a test-only override,
    // not a weaker default. ALKAROS_LOGIN_RATE_LIMIT_PERMITS lets the E2E
    // Host process opt into a generous limit; unset (every real deployment)
    // keeps today's 10/minute exactly as-is.
    private static int LoginRateLimitPermits()
        => int.TryParse(Environment.GetEnvironmentVariable("ALKAROS_LOGIN_RATE_LIMIT_PERMITS"), out var configured) && configured > 0
            ? configured
            : 10;

    private static string RoutePartition(HttpContext context, string routeName, string operation)
    {
        var routeValue = context.Request.RouteValues.TryGetValue(routeName, out var value)
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;
        return $"{ClientPartition(context)}:{operation}:{routeValue ?? "missing"}";
    }

    /// <summary>
    /// V12-CWB-001: same partitioning idea as <see cref="RoutePartition"/>
    /// but keyed by a request header instead of a route value — a QR
    /// customer's session token has no route segment of its own to key on.
    /// Headers (unlike the request body) are available before rate limiting
    /// runs, so this needs no special handler-side wiring.
    /// </summary>
    private static string HeaderPartition(HttpContext context, string headerName, string operation)
    {
        var headerValue = context.Request.Headers[headerName].ToString();
        return $"{operation}:{(string.IsNullOrEmpty(headerValue) ? "missing" : headerValue)}";
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
            OrderNotFoundException => (404, "ORDER_NOT_FOUND", "Sipariş bulunamadı."),
            // V13-CSH-004: Cash/CashTender domain exceptions, most specific
            // first (CashVarianceThresholdExceededException/
            // ActiveCashSessionExistsException/InvalidCashSessionStateException
            // all derive from CashSessionException, so they must precede it).
            CashSessionNotFoundException => (404, "CASH_SESSION_NOT_FOUND", "Kasa oturumu bulunamadı."),
            ActiveCashSessionExistsException => (409, "ACTIVE_CASH_SESSION_EXISTS", "Bu terminalde zaten açık bir kasa oturumu var."),
            InvalidCashSessionStateException => (409, "INVALID_CASH_SESSION_STATE", "Kasa oturumu bu işlem için uygun durumda değil."),
            CashVarianceThresholdExceededException => (409, "CASH_VARIANCE_THRESHOLD_EXCEEDED", "Fark tolerans sınırını aşıyor; süpervizör onayı gerekiyor."),
            NegativeCashAmountException => (400, "VALIDATION_FAILED", "Tutar negatif olamaz."),
            CashSessionException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            CashTenderBillNotFoundException => (404, "BILL_NOT_FOUND", "Hesap bulunamadı."),
            ClosedCashSessionException => (409, "CLOSED_CASH_SESSION", "Kasa oturumu açık değil."),
            InsufficientCashTenderException => (400, "INSUFFICIENT_CASH_TENDER", "Verilen tutar hesaplanan tutarı karşılamıyor."),
            CashTenderException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
            // V13-CSH-004: OverAllocationException (V13-ALC-001) is reachable
            // through the cash-tender endpoint - both this handler's own
            // fail-fast check and AllocateAsync's deeper, lock-guarded one
            // can throw it when the requested amount exceeds what the bill
            // actually has left.
            OverAllocationException => (409, "OVER_ALLOCATION", "İstenen tutar hesabın kalan bakiyesini aşıyor."),
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
