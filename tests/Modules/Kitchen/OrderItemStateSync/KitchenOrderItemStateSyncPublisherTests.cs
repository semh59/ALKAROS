using ALKAROS.Kitchen.OrderItemStateSync.Tests.Fixtures;
using ALKAROS.Kitchen.TicketLifecycle;
using Npgsql;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Kitchen.OrderItemStateSync.Tests;

public sealed class KitchenOrderItemStateSyncPublisherTests : IClassFixture<OutboxTestDatabase>
{
    private readonly OutboxTestDatabase _db;

    public KitchenOrderItemStateSyncPublisherTests(OutboxTestDatabase db)
    {
        _db = db;
    }

    private static KitchenTicketItem NewItem(KitchenTicketItemState status)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Lahmacun", 1, status: status);

    [Fact]
    public async Task DisabledNeverEnqueuesAnything()
    {
        var item = NewItem(KitchenTicketItemState.Preparing);

        await PublishInTransactionAsync(Guid.NewGuid(), item, enabled: false, commit: true);

        (await CountOutboxRowsAsync(item.Id)).Should().Be(0);
    }

    [Theory]
    [InlineData(KitchenTicketItemState.Queued)]
    [InlineData(KitchenTicketItemState.Preparing)]
    [InlineData(KitchenTicketItemState.Ready)]
    [InlineData(KitchenTicketItemState.Served)]
    [InlineData(KitchenTicketItemState.Cancelled)]
    public async Task EnabledEnqueuesTheStateForEveryReachableStatus(KitchenTicketItemState status)
    {
        var item = NewItem(status);

        await PublishInTransactionAsync(Guid.NewGuid(), item, enabled: true, commit: true);

        (await CountOutboxRowsAsync(item.Id)).Should().Be(1);
    }

    [Fact]
    public async Task ThrowsForAnEmptyOrderId()
        => await FluentActions
            .Invoking(() => PublishInTransactionAsync(
                Guid.Empty, NewItem(KitchenTicketItemState.Ready), enabled: true, commit: true))
            .Should().ThrowAsync<ArgumentException>();

    [Fact]
    public async Task ARolledBackTransactionTakesTheEventWithIt()
    {
        var item = NewItem(KitchenTicketItemState.Ready);

        await PublishInTransactionAsync(Guid.NewGuid(), item, enabled: true, commit: false);

        (await CountOutboxRowsAsync(item.Id)).Should().Be(0);
    }

    private async Task PublishInTransactionAsync(Guid orderId, KitchenTicketItem item, bool enabled, bool commit)
    {
        await using var connection = await _db.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await KitchenOrderItemStateSyncPublisher.PublishAsync(orderId, item, enabled, connection, transaction);
        if (commit)
            await transaction.CommitAsync();
    }

    private async Task<long> CountOutboxRowsAsync(Guid aggregateId)
    {
        await using var command = _db.DataSource.CreateCommand(
            "SELECT count(*) FROM outbox_messages WHERE aggregate_id = @id;");
        command.Parameters.AddWithValue("id", aggregateId);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
