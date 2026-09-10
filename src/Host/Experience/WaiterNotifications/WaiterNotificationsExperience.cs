using ALKAROS.Host.Experience.PendingOrderNotifications;
using ALKAROS.Orders.Integration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ALKAROS.Host.Experience.WaiterNotifications;

public static class WaiterNotificationsExperience
{
    public static IServiceCollection AddWaiterNotificationsExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // AddSignalR is idempotent to call more than once (last registration
        // wins for options; the standalone Host.Experience test harnesses in
        // this codebase each register their own dependencies defensively —
        // see OfflineReconciliation/AddOfflineReconciliationExperience).
        services.AddSignalR(options => options.EnableDetailedErrors = false);
        // V1-RMD-149: lets Orders announce a guest order waiting for
        // confirmation without depending on SignalR or on Host at all.
        services.TryAddSingleton<IPendingOrderAnnouncer, SignalRPendingOrderAnnouncer>();
        return services;
    }

    public static IEndpointRouteBuilder MapWaiterNotificationsApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapHub<WaiterOrderStatusHub>(WaiterOrderStatusHub.Route);
        return endpoints;
    }
}
