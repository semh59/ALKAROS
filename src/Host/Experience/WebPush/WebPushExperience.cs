using ALKAROS.Host.DualScreen;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ALKAROS.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ALKAROS.Host.Experience.WebPush;

/// <summary>
/// V1-WTR-011: registration and the three endpoints a device needs to be
/// reachable while its app is closed.
/// </summary>
public static class WebPushExperience
{
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/push";

    public static IServiceCollection AddWebPushExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // The VAPID private key is stored as an envelope under the master key from the environment.
        services.TryAddTransient<ISecretProvider, EnvironmentVariableSecretProvider>();
        services.TryAddSingleton<PushSubscriptionStore>();
        // Same reason the other four Experience areas each register it: this
        // one needs it only to authenticate the cashier session on its own
        // endpoints, and TryAdd keeps a shared composition to one instance.
        services.TryAddSingleton<DualScreenStore>();
        // Its own long-lived client rather than the ambient one: a push service that hangs must not hold a
        // request thread, a notification is never worth waiting ten seconds for, redirects are not followed
        // (a push service has no reason to send this host elsewhere), and the connection only ever goes to
        // a public address. The pooled connections are refreshed so a changed DNS answer is picked up.
        services.TryAddSingleton(provider => new WebPushSender(
            new HttpClient(PushEndpointPolicy.CreateHandler()) { Timeout = TimeSpan.FromSeconds(10) },
            provider.GetRequiredService<PushSubscriptionStore>(),
            provider.GetRequiredService<ILogger<WebPushSender>>()));
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(WebPushDispatchService)))
        {
            services.AddSingleton<WebPushDispatchService>();
            services.AddHostedService(provider => provider.GetRequiredService<WebPushDispatchService>());
        }

        return services;
    }

    public static IEndpointRouteBuilder MapWebPushApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // V1-RMD-163: found by the 2026-09-10 Garson audit — this group had
        // no rate limiting at all, unlike DualScreenApplication's own
        // endpoints and Orders (both reuse these exact same
        // globally-registered "terminal-read"/"terminal-write" policies —
        // AddRateLimiter is called once for the whole app).
        var group = endpoints.MapGroup(RoutePrefix).WithTags("WebPush")
            .RequireRateLimiting("terminal-write");

        // The VAPID public key. A client cannot call pushManager.subscribe
        // without it, and it is public by definition — but the session check
        // stays, because only a signed-in device has any use for it.
        group.MapGet("/public-key", async (
            Guid terminalId,
            WebPushSender sender,
            DualScreenStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierAsync(context, terminalId, store, cancellationToken);
            return Results.Ok(new PushPublicKeyResponseV1(await sender.GetPublicKeyAsync(cancellationToken)));
        }).RequireRateLimiting("terminal-read");

        group.MapPost("/subscriptions", async (
            Guid terminalId,
            SavePushSubscriptionRequestV1 request,
            PushSubscriptionStore store,
            DualScreenStore sessions,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierAsync(context, terminalId, sessions, cancellationToken);

            if (request is null
                || string.IsNullOrWhiteSpace(request.Endpoint)
                || string.IsNullOrWhiteSpace(request.P256dh)
                || string.IsNullOrWhiteSpace(request.Auth))
            {
                return Results.BadRequest(new { error = new { code = "VALIDATION_FAILED", message = "Abonelik bilgileri eksik." } });
            }

            if (!PushEndpointPolicy.IsAcceptable(request.Endpoint))
            {
                // Every real push service is an https name on the default port. Refusing anything else keeps
                // this endpoint from being turned into a request forwarder pointed at an internal address.
                return Results.BadRequest(new { error = new { code = "VALIDATION_FAILED", message = "Abonelik adresi geçersiz." } });
            }

            // Checking both keys here rather than at send time means a
            // malformed subscription is rejected while someone can still see
            // the error, instead of silently never receiving anything.
            if (!WebPushCrypto.AreValidSubscriptionKeys(request.P256dh, request.Auth))
            {
                return Results.BadRequest(new { error = new { code = "VALIDATION_FAILED", message = "Abonelik anahtarları çözümlenemedi." } });
            }

            var saved = await store.SaveAsync(
                new PushSubscriptionRecord(
                    Guid.NewGuid(), request.Endpoint, request.P256dh, request.Auth, principal.UserId, terminalId),
                cancellationToken);
            return saved
                ? Results.NoContent()
                : Results.Conflict(new { error = new { code = "PUSH_ENDPOINT_IN_USE", message = "Bu cihaz adresi başka bir kullanıcıya ait." } });
        });

        // V1-RMD-160: found by the 2026-09-10 Garson audit — this used to
        // call DeleteByEndpointAsync, which deletes any row with that
        // endpoint string, no owner check. Scoped to the caller's own
        // user_id now, so a session can only ever remove its own device's
        // subscription.
        group.MapDelete("/subscriptions", async (
            Guid terminalId,
            string endpoint,
            PushSubscriptionStore store,
            DualScreenStore sessions,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await RequireCashierAsync(context, terminalId, sessions, cancellationToken);
            await store.DeleteByEndpointForUserAsync(endpoint, principal.UserId, cancellationToken);
            return Results.NoContent();
        });

        return endpoints;
    }

    private static async Task<CashierPrincipal> RequireCashierAsync(
        HttpContext context, Guid terminalId, DualScreenStore store, CancellationToken cancellationToken)
    {
        var cashierToken = context.Request.Cookies[DualScreenApplication.CashierCookieName];
        var principal = await store.AuthenticateCashierAsync(cashierToken, terminalId, cancellationToken);
        return principal ?? throw new DualScreenUnauthorizedException("Cashier authentication is required.");
    }
}

/// <summary>V1-WTR-011: what a browser's PushSubscription reports, as sent.</summary>
public sealed record SavePushSubscriptionRequestV1(string Endpoint, string P256dh, string Auth);

/// <summary>V1-WTR-011: the VAPID public key, base64url.</summary>
public sealed record PushPublicKeyResponseV1(string PublicKey);
