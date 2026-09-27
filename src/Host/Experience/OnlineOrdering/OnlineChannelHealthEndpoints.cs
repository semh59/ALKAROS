using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.OnlineOrdering.Credentials;
using ALKAROS.OnlineOrdering.Providers.Contracts;
using ALKAROS.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.OnlineOrdering;

/// <summary>
/// V12-OUI-004: the online food screen's platform status line — per registered platform, whether its connection settings
/// are entered (on the settings screen or as environment variables), when its last order event arrived and whether its
/// order polling is failing. Staff who take orders see it (<c>orders.create</c>); no secret, raw error text or payload is
/// returned, only what the screen turns into Turkish.
/// </summary>
public static class OnlineChannelHealthEndpoints
{
    public const string Route = "/api/v1/terminals/{terminalId:guid}/online-channels";

    /// <summary>
    /// The settings a platform's order connection cannot work without (the settings screen's other fields are optional
    /// or serve other features). Unknown platforms are reported as not configured rather than guessed.
    /// </summary>
    private static readonly Dictionary<string, string[]> RequiredSettings = new(StringComparer.Ordinal)
    {
        ["yemeksepeti"] = ["api-base-url", "chain-id", "vendor-id", "client-id", "client-secret", "webhook-secret"],
        ["trendyol-go"] = ["api-base-url", "supplier-id", "api-key", "api-secret", "integrator-name", "executor-email"],
    };

    public static IServiceCollection AddOnlineChannelHealthExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DualScreenStore>();
        services.TryAddSingleton<IRoleRepository, PostgresRoleRepository>();
        services.TryAddSingleton<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddSingleton<IAuthorizationService, AuthorizationService>();
        services.TryAddTransient<ISecretProvider, EnvironmentVariableSecretProvider>();
        services.TryAddTransient<IOnlinePlatformCredentialStore, PostgresOnlinePlatformCredentialStore>();
        services.TryAddTransient<OnlinePlatformCredentialExceptionFilter>();
        return services;
    }

    public static RouteHandlerBuilder MapOnlineChannelHealthApi(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(Route, async (
            Guid terminalId,
            NpgsqlDataSource dataSource,
            OnlineOrderProviderRegistry providers,
            IOnlinePlatformCredentialStore credentials,
            ISecretProvider environment,
            DualScreenStore dualStore,
            IAuthorizationService authorization,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await RequireStaffAsync(context, terminalId, dualStore, authorization, cancellationToken);
            var settings = new StoredOnlinePlatformSecretProvider(credentials, environment);
            var activity = await ReadActivityAsync(dataSource, cancellationToken);
            var channels = providers.Providers
                .Order(StringComparer.Ordinal)
                .Select(id =>
                {
                    var platform = providers.Get(id);
                    // A platform with no event and no polling yet has simply never been heard from.
                    var seen = activity.TryGetValue(id, out var found) ? found : (null, OnlineChannelPolling.NotPolled);
                    return new OnlineChannelHealthV1(
                        id,
                        platform.DisplayName,
                        RequiredSettings.TryGetValue(id, out var required)
                            && required.All(field => settings.GetValue(new SecretReference($"{id}-{field}")) is not null),
                        seen.LastEventAt,
                        seen.Polling);
                })
                .ToList();
            return Results.Ok(new OnlineChannelsHealthV1(channels));
        })
        .RequireRateLimiting("terminal-read")
        .AddEndpointFilter<OnlinePlatformCredentialExceptionFilter>();

    private static async Task<Dictionary<string, (DateTimeOffset? LastEventAt, string Polling)>> ReadActivityAsync(
        NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            WITH events AS (SELECT provider, max(received_at) AS last_event_at FROM online_ordering.provider_inbox GROUP BY provider)
            SELECT coalesce(e.provider, p.provider), e.last_event_at, p.consecutive_failures, p.last_error
            FROM events e
            FULL JOIN online_ordering.provider_poll_state p ON p.provider = e.provider;
            """);
        var activity = new Dictionary<string, (DateTimeOffset?, string)>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var polling = reader.IsDBNull(2)
                ? OnlineChannelPolling.NotPolled
                : reader.GetInt32(2) == 0
                    ? OnlineChannelPolling.Working
                    : !reader.IsDBNull(3) && reader.GetString(3) == "RateLimited"
                        ? OnlineChannelPolling.RateLimited
                        : OnlineChannelPolling.Failing;
            activity[reader.GetString(0)] = (reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1), polling);
        }

        return activity;
    }

    private static async Task RequireStaffAsync(
        HttpContext context, Guid terminalId, DualScreenStore store, IAuthorizationService authorization, CancellationToken cancellationToken)
    {
        var cashierToken = context.Request.Cookies[DualScreenApplication.CashierCookieName];
        if (string.IsNullOrWhiteSpace(cashierToken))
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                cashierToken = authHeader["Bearer ".Length..].Trim();
        }

        var principal = await store.AuthenticateCashierAsync(cashierToken, terminalId, cancellationToken)
            ?? throw new DualScreenUnauthorizedException("Cashier authentication is required.");
        await authorization.AuthorizeAsync(principal.UserId, ApplicationPermissions.OrdersCreate, cancellationToken);
    }
}

/// <summary>How a platform's order polling is doing; the screen shows each in Turkish.</summary>
public static class OnlineChannelPolling
{
    public const string NotPolled = "NotPolled";
    public const string Working = "Working";
    public const string RateLimited = "RateLimited";
    public const string Failing = "Failing";
}

public sealed record OnlineChannelHealthV1(
    string Provider, string DisplayName, bool Configured, DateTimeOffset? LastEventAt, string Polling);

public sealed record OnlineChannelsHealthV1(IReadOnlyList<OnlineChannelHealthV1> Platforms);
