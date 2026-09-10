using ALKAROS.Host.DualScreen;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.TryAddSingleton<PushSubscriptionStore>();
        // Same reason the other four Experience areas each register it: this
        // one needs it only to authenticate the cashier session on its own
        // endpoints, and TryAdd keeps a shared composition to one instance.
        services.TryAddSingleton<DualScreenStore>();
        // A named client rather than the ambient one: a push service that
        // hangs must not hold a request thread, and a notification is never
        // worth waiting ten seconds for.
        services.AddHttpClient<WebPushSender>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        return services;
    }

    public static IEndpointRouteBuilder MapWebPushApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup(RoutePrefix).WithTags("WebPush");

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
        });

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

            if (!Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var endpoint)
                || endpoint.Scheme != Uri.UriSchemeHttps)
            {
                // Every real push service is https. Refusing anything else
                // keeps this endpoint from being turned into a request
                // forwarder pointed at an internal address.
                return Results.BadRequest(new { error = new { code = "VALIDATION_FAILED", message = "Abonelik adresi geçersiz." } });
            }

            // Parsing both keys here rather than at send time means a
            // malformed subscription is rejected while someone can still see
            // the error, instead of silently never receiving anything.
            try
            {
                _ = WebPushCrypto.FromBase64Url(request.P256dh);
                _ = WebPushCrypto.FromBase64Url(request.Auth);
            }
            catch (FormatException)
            {
                return Results.BadRequest(new { error = new { code = "VALIDATION_FAILED", message = "Abonelik anahtarları çözümlenemedi." } });
            }

            await store.SaveAsync(
                new PushSubscriptionRecord(
                    Guid.NewGuid(), request.Endpoint, request.P256dh, request.Auth, principal.UserId, terminalId),
                cancellationToken);
            return Results.NoContent();
        });

        group.MapDelete("/subscriptions", async (
            Guid terminalId,
            string endpoint,
            PushSubscriptionStore store,
            DualScreenStore sessions,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireCashierAsync(context, terminalId, sessions, cancellationToken);
            await store.DeleteByEndpointAsync(endpoint, cancellationToken);
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
