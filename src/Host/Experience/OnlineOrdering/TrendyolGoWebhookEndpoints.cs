using ALKAROS.OnlineOrdering.Providers.Inbox;
using ALKAROS.OnlineOrdering.Providers.TrendyolGo.OrderIntake;
using ALKAROS.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ALKAROS.Host.Experience.OnlineOrdering;

/// <summary>
/// V12-TGO-002: the Trendyol Go webhook. The integrator is registered with <c>baseUrl</c> = this route's prefix and one
/// <c>destinationUrl</c> per event (<c>/created</c>, <c>/cancelled</c>, <c>/unsupplied</c>, <c>/shipped</c>,
/// <c>/delivered</c>) and the header <see cref="TrendyolGoWebhookInbox.HeaderName"/> carrying the platform's
/// webhook secret. The caller is refused before its body is read; a stored or repeated event answers 200.
/// UNVERIFIED DRAFT (EXT:TGO-MEAL-API; V12-TGO-001 Blocked, C106 waiver).
/// </summary>
public static class TrendyolGoWebhookEndpoints
{
    public const string RoutePrefix = "/api/v1/integrations/trendyol-go/orders/webhook";

    public static IServiceCollection AddTrendyolGoWebhookExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // TryAdd defers to OnlineOrderingModule in the real Host.
        services.TryAddTransient<ISecretProvider, EnvironmentVariableSecretProvider>();
        services.TryAddTransient<ProviderInbox>();
        services.TryAddTransient(provider => new TrendyolGoWebhookInbox(
            provider.GetRequiredService<ProviderInbox>(), provider.GetRequiredService<ISecretProvider>()));
        return services;
    }

    public static RouteHandlerBuilder MapTrendyolGoWebhookApi(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(RoutePrefix + "/{eventType}", async (
            string eventType, HttpContext context, TrendyolGoWebhookInbox inbox, CancellationToken cancellationToken) =>
        {
            var presented = context.Request.Headers[TrendyolGoWebhookInbox.HeaderName].ToString();
            if (inbox.Authenticate(presented) is { } refused)
                return refused == TrendyolGoWebhookOutcome.ChannelNotConfigured
                    ? Reply(StatusCodes.Status503ServiceUnavailable, "CHANNEL_NOT_CONFIGURED")
                    : Reply(StatusCodes.Status401Unauthorized, "UNAUTHENTICATED");
            if (!TrendyolGoEvents.WebhookEventTypes.Contains(eventType))
                return Reply(StatusCodes.Status404NotFound, "UNKNOWN_EVENT_TYPE");
            if (context.Request.ContentLength is > TrendyolGoWebhookInbox.MaxBodyBytes)
                return Reply(StatusCodes.Status413PayloadTooLarge, "PAYLOAD_TOO_LARGE");

            var body = await ReadBoundedAsync(context.Request.Body, cancellationToken).ConfigureAwait(false);
            var (outcome, _) = await inbox.ReceiveAsync(eventType, presented, body, cancellationToken).ConfigureAwait(false);
            return outcome switch
            {
                TrendyolGoWebhookOutcome.Stored => Results.Ok(new { status = "stored" }),
                TrendyolGoWebhookOutcome.Duplicate => Results.Ok(new { status = "duplicate" }),
                TrendyolGoWebhookOutcome.ChannelNotConfigured => Reply(StatusCodes.Status503ServiceUnavailable, "CHANNEL_NOT_CONFIGURED"),
                TrendyolGoWebhookOutcome.Unauthenticated => Reply(StatusCodes.Status401Unauthorized, "UNAUTHENTICATED"),
                TrendyolGoWebhookOutcome.UnknownEventType => Reply(StatusCodes.Status404NotFound, "UNKNOWN_EVENT_TYPE"),
                TrendyolGoWebhookOutcome.TooLarge => Reply(StatusCodes.Status413PayloadTooLarge, "PAYLOAD_TOO_LARGE"),
                TrendyolGoWebhookOutcome.Malformed => Reply(StatusCodes.Status400BadRequest, "MALFORMED_PAYLOAD"),
                _ => throw new InvalidOperationException($"Unhandled webhook outcome '{outcome}'.")
            };
        }).RequireRateLimiting("trendyol-go-webhook");

    // A platform-to-server response: a machine-readable code, never shown to a person.
    private static IResult Reply(int statusCode, string code) => Results.Json(new { code }, statusCode: statusCode);

    /// <summary>Reads at most one byte past the limit, so a chunked body without a length cannot exhaust memory.</summary>
    private static async Task<ReadOnlyMemory<byte>> ReadBoundedAsync(Stream body, CancellationToken cancellationToken)
    {
        var buffer = new byte[TrendyolGoWebhookInbox.MaxBodyBytes + 1];
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
