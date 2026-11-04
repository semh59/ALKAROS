using ALKAROS.Audit.EventStore;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.CrossChannelReservation;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.MovementReversal;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Tables.TableRelease;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ALKAROS.Host.Experience.Orders.OrderCancellation;

public static class OrderCancellationRegistration
{
    /// <summary>
    /// Everything the whole-order cancellation and the single-line void need; TryAdd defers to the module
    /// registrations in the real Host, so a standalone composition of one experience resolves the whole chain.
    /// </summary>
    public static IServiceCollection AddOrderCancellation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IAuditEventStore, PostgresAuditEventStore>();
        services.TryAddSingleton<IOrderRepository, PostgresOrderRepository>();
        services.TryAddSingleton<IBillRepository, PostgresBillRepository>();
        services.TryAddSingleton<IKitchenTicketRepository, PostgresKitchenTicketRepository>();
        services.TryAddSingleton<IStockMovementRepository, PostgresStockMovementRepository>();
        services.TryAddSingleton<IStockItemRepository, PostgresStockItemRepository>();
        services.TryAddSingleton<IStockLocationRepository, PostgresStockLocationRepository>();
        services.TryAddSingleton<IStockBalanceRepository, PostgresStockBalanceRepository>();
        services.TryAddSingleton<IStockBalanceProjector, StockBalanceProjector>();
        services.TryAddSingleton<IStockMovementReversalService, StockMovementReversalService>();
        services.TryAddSingleton<IReservationAwareConsumptionGuard, PostgresReservationAwareConsumptionGuard>();
        services.TryAddSingleton<ITableReleasePort, PostgresTableReleasePort>();
        services.TryAddSingleton<OrderItemVoidEffects>();
        services.TryAddSingleton<OrderCancellationStore>();
        return services;
    }
}
