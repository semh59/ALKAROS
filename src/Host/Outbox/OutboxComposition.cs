using ALKAROS.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Outbox;

/// <summary>
/// Wires the outbox dispatch pipeline into the serve host: the
/// <see cref="OutboxStore"/> over the shared data source, the
/// <see cref="OutboxFanoutSink"/> that fans a message out to the module
/// <c>IIntegrationEventConsumer</c> registrations, and the single background
/// worker that drains the table. Module consumers register themselves through
/// the module catalog, so nothing module-specific is named here.
/// </summary>
public static class OutboxComposition
{
    public static IServiceCollection AddOutboxDispatch(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(sp => new OutboxStore(sp.GetRequiredService<NpgsqlDataSource>()));
        services.TryAddScoped<IOutboxDeliverySink, OutboxFanoutSink>();
        services.AddHostedService<OutboxDispatcherHostedService>();

        return services;
    }
}
