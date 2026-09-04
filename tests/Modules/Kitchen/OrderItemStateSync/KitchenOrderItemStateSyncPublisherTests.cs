using ALKAROS.Kitchen.OrderItemStateSync.Tests.Fixtures;
using ALKAROS.Kitchen.TicketLifecycle;
using ALKAROS.Messaging;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Kitchen.OrderItemStateSync.Tests;

public sealed class KitchenOrderItemStateSyncPublisherTests : IClassFixture<OutboxTestDatabase>
{
    private readonly OutboxTestDatabase _db;
    private readonly OutboxStore _outbox;

    public KitchenOrderItemStateSyncPublisherTests(OutboxTestDatabase db)
    {
        _db = db;
        _outbox = new OutboxStore(db.DataSource);
    }

    private static KitchenTicketItem NewItem(KitchenTicketItemState status)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Lahmacun", 1, status: status);

    [Fact]
    public async Task DisabledNeverEnqueuesAnything()
    {
        var item = NewItem(KitchenTicketItemState.Preparing);

        await KitchenOrderItemStateSyncPublisher.PublishAsync(_outbox, Guid.NewGuid(), item, enabled: false);

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

        await KitchenOrderItemStateSyncPublisher.PublishAsync(_outbox, Guid.NewGuid(), item, enabled: true);

        (await CountOutboxRowsAsync(item.Id)).Should().Be(1);
    }

    [Fact]
    public async Task ThrowsForAnEmptyOrderId()
        => await FluentActions
            .Invoking(() => KitchenOrderItemStateSyncPublisher.PublishAsync(
                _outbox, Guid.Empty, NewItem(KitchenTicketItemState.Ready), enabled: true))
            .Should().ThrowAsync<ArgumentException>();

    private async Task<long> CountOutboxRowsAsync(Guid aggregateId)
    {
        await using var command = _db.DataSource.CreateCommand(
            "SELECT count(*) FROM outbox_messages WHERE aggregate_id = @id;");
        command.Parameters.AddWithValue("id", aggregateId);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
