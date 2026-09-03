using ALKAROS.IntegrationContracts;
using ALKAROS.Messaging;
using Npgsql;

namespace ALKAROS.TestHelpers;

/// <summary>
/// Runs the outbox dispatch loop synchronously in a test: claims every pending
/// <c>outbox_messages</c> row and delivers it through an
/// <see cref="OutboxFanoutSink"/> over the given consumers, the same path the
/// production <c>OutboxDispatcherHostedService</c> uses. Tests that assert the
/// eventual effect of a transactional-outbox event call this right after the
/// producing operation instead of waiting on the background worker.
/// </summary>
public static class OutboxTestDrain
{
    public static async Task<int> DrainAsync(
        NpgsqlDataSource dataSource,
        IEnumerable<IIntegrationEventConsumer> consumers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        var store = new OutboxStore(dataSource);
        var sink = new OutboxFanoutSink(consumers);

        var total = 0;
        int attempted;
        do
        {
            attempted = await store.DispatchAsync(sink, batchSize: 50, cancellationToken).ConfigureAwait(false);
            total += attempted;
        }
        while (attempted > 0);

        return total;
    }
}
