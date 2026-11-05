using ALKAROS.Host.Experience.Orders.OrderCancellation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ALKAROS.Host.Experience.Orders.OrderCleanup;

public static class OrderCleanupRegistration
{
    public static IServiceCollection AddOrderCleanup(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOrderCancellation();
        services.TryAddSingleton<OrderCleanupStore>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, EmptyOrderSweeper>());
        return services;
    }
}
