using ALKAROS.Host.DualScreen;
using ALKAROS.Settings.GarsonFeatureToggles;
using ALKAROS.Settings.TypedSettings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using System.Data.Common;

namespace ALKAROS.Host.Experience.HelpRequests;

/// <summary>
/// V1-WTR-014: a waiter's real-time call for help, raised from the same
/// cashier session <c>Experience/Orders</c> uses, delivered to every
/// connected manager/supervisor session over <see cref="HelpRequestHub"/>.
/// Its own area (not folded into Orders) for the same reason
/// WaiterNotifications is not folded into Orders either: the recipient side
/// authenticates against a completely different cookie/session model
/// (<c>alkaros.manager</c>, not the cashier cookie).
/// </summary>
public static class HelpRequestExperience
{
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/help-requests";

    public static IServiceCollection AddHelpRequestExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // AddSignalR is idempotent to call more than once (see
        // WaiterNotificationsExperience's own note on this).
        services.AddSignalR(options => options.EnableDetailedErrors = false);
        services.TryAddSingleton<DualScreenStore>();
        // V1-SET-004: HelpRequestStore now checks GarsonFeature.HelpRequest —
        // same DbDataSource/ISettingValidator gap KitchenOperationsEndpoints
        // and OrderManagementEndpoints already had to close for their own
        // settings-backed features.
        services.TryAddSingleton<DbDataSource>(serviceProvider =>
            serviceProvider.GetRequiredService<NpgsqlDataSource>());
        services.TryAddSingleton<ISettingValidator, SettingValidator>();
        services.TryAddSingleton<ISettingsRepository, PostgresSettingsRepository>();
        services.TryAddSingleton<ISettingsService, SettingsService>();
        services.TryAddSingleton<HelpRequestStore>();
        return services;
    }

    public static IEndpointRouteBuilder MapHelpRequestApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapHub<HelpRequestHub>(HelpRequestHub.Route);

        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("HelpRequests")
            .RequireRateLimiting("terminal-write");

        group.MapPost("", async (
            Guid terminalId,
            HelpRequestV1 request,
            HelpRequestStore store,
            IHubContext<HelpRequestHub> hub,
            DualScreenStore dualStore,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var cashierToken = context.Request.Cookies[DualScreenApplication.CashierCookieName];
            var principal = await dualStore.AuthenticateCashierAsync(cashierToken, terminalId, cancellationToken);
            if (principal is null)
                return Results.Json(
                    new { error = new { code = "UNAUTHORIZED", message = "Oturum geçersiz veya süresi dolmuş." } },
                    statusCode: StatusCodes.Status401Unauthorized);

            if (!HelpRequestTypeCatalog.IsValid(request.RequestType))
                return Results.BadRequest(new
                {
                    error = new { code = "VALIDATION_FAILED", message = "Geçersiz yardım türü." },
                });

            HelpRequestRecord record;
            try
            {
                record = await store.RaiseAsync(request.TableId, request.RequestType, principal.UserId, cancellationToken);
            }
            catch (HelpRequestCooldownActiveException)
            {
                return Results.Json(
                    new
                    {
                        error = new
                        {
                            code = "HELP_REQUEST_COOLDOWN",
                            message = "Bu masa için az önce zaten yardım çağrıldı, lütfen biraz bekleyin.",
                        },
                    },
                    statusCode: StatusCodes.Status429TooManyRequests);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Masa bulunamadı." } });
            }
            catch (GarsonFeatureDisabledException)
            {
                return Results.Json(
                    new { error = new { code = "FEATURE_DISABLED", message = "Bu özellik bu işletme için kapatılmış." } },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            // V1-RMD-289: explicit recipient group instead of Clients.All - see HelpRequestHub's own doc
            // comment on why this stays a flat, restaurant-wide broadcast rather than a per-terminal one.
            await hub.Clients.Group(HelpRequestHub.RecipientsGroup).SendAsync(
                HelpRequestHub.HelpRequested,
                new HelpRequestedV1(request.TableId, record.TableNumber, request.RequestType,
                    record.RequestedByDisplayName, record.CreatedAt),
                cancellationToken);

            return Results.Ok(new HelpRequestedV1(
                request.TableId, record.TableNumber, request.RequestType,
                record.RequestedByDisplayName, record.CreatedAt));
        });

        return endpoints;
    }
}
