using ALKAROS.OnlineOrdering.Yemeksepeti.WebhookInbox;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ALKAROS.Host.Experience.OnlineOrdering;

/// <summary>
/// V12-ONL-001: the public endpoint Yemeksepeti's order webhook would call.
///
/// UNVERIFIED DRAFT: the route is ours, but everything provider-facing (the secret arriving
/// in the Authorization header, the 10-second timeout, retries on any non-2xx) follows the
/// public Partner API v2.0.2 document only; it has never received a real delivery
/// (V0-YSP-001 is Blocked). With no webhook secret configured the endpoint answers 503
/// and reads nothing, so the channel is off until an operator deliberately turns it on.
/// A delivery is acknowledged with 200 only after it is durably stored (or was already
/// stored), which makes the provider's retries safe.
/// </summary>
public static class YemeksepetiWebhookEndpoints
{
    public const string Route = "/api/v1/integrations/yemeksepeti/orders/webhook";

    public static IServiceCollection AddYemeksepetiWebhookExperience(this IServiceCollection services)
    {
        // Same TryAdd-defers-to-the-module shape as the other experiences: the real Host
        // registers these through OnlineOrderingModule.
        services.TryAddTransient<ALKAROS.Secrets.ISecretProvider, ALKAROS.Secrets.EnvironmentVariableSecretProvider>();
        services.TryAddTransient<YemeksepetiWebhookInbox>();
        return services;
    }

    public static RouteHandlerBuilder MapYemeksepetiWebhookApi(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(Route, async (HttpContext context, YemeksepetiWebhookInbox inbox, CancellationToken cancellationToken) =>
        {
            if (context.Request.ContentLength is > YemeksepetiWebhookInbox.MaxBodyBytes)
                return Reply(StatusCodes.Status413PayloadTooLarge, "PAYLOAD_TOO_LARGE");

            var body = await ReadBoundedAsync(context.Request.Body, cancellationToken).ConfigureAwait(false);
            var receipt = await inbox.ReceiveAsync(context.Request.Headers.Authorization.ToString(), body, cancellationToken)
                .ConfigureAwait(false);

            return receipt.Outcome switch
            {
                WebhookReceiptOutcome.Stored => Results.Ok(new { status = "stored" }),
                WebhookReceiptOutcome.Duplicate => Results.Ok(new { status = "duplicate" }),
                WebhookReceiptOutcome.ChannelNotConfigured => Reply(StatusCodes.Status503ServiceUnavailable, "CHANNEL_NOT_CONFIGURED"),
                WebhookReceiptOutcome.Unauthenticated => Reply(StatusCodes.Status401Unauthorized, "UNAUTHENTICATED"),
                WebhookReceiptOutcome.TooLarge => Reply(StatusCodes.Status413PayloadTooLarge, "PAYLOAD_TOO_LARGE"),
                WebhookReceiptOutcome.Malformed => Reply(StatusCodes.Status400BadRequest, "MALFORMED_PAYLOAD"),
                _ => throw new InvalidOperationException($"Unhandled webhook outcome '{receipt.Outcome}'.")
            };
        }).RequireRateLimiting("yemeksepeti-webhook");

    // A provider-to-server response: a machine-readable code, never shown to a person.
    private static IResult Reply(int statusCode, string code) => Results.Json(new { code }, statusCode: statusCode);

    /// <summary>Reads at most one byte past the limit, so a chunked body without a length cannot exhaust memory.</summary>
    private static async Task<ReadOnlyMemory<byte>> ReadBoundedAsync(Stream body, CancellationToken cancellationToken)
    {
        var buffer = new byte[YemeksepetiWebhookInbox.MaxBodyBytes + 1];
        var total = 0;
        int read;
        while (total < buffer.Length
               && (read = await body.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
        }

        return buffer.AsMemory(0, total);
    }
}
