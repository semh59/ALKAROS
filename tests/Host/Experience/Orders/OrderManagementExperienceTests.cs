using ALKAROS.Host.Experience.Orders;
using Xunit;

namespace ALKAROS.Host.Tests.Experience.Orders;

public sealed class OrderManagementExperienceTests
{
    private readonly OrderManagementStore _store = new();

    [Fact]
    public async Task CreateOrUpdateTableDraftAsync_ValidRequest_CreatesDraftWithCalculatedTotals()
    {
        var tableId = Guid.NewGuid();
        var request = new CreateTableDraftRequest(
            tableId,
            "M-05",
            "Garson Ahmet",
            new List<OrderItemDraftDto>
            {
                new(Guid.NewGuid(), "Köfte", 2, 280m),
                new(Guid.NewGuid(), "Ayran", 2, 40m)
            }
        );

        var draft = await _store.CreateOrUpdateTableDraftAsync(request);

        Assert.NotNull(draft);
        Assert.Equal(tableId, draft.TableId);
        Assert.Equal("M-05", draft.TableNumber);
        Assert.Equal("Draft", draft.Status);
        Assert.Equal(2, draft.Items.Count);
        Assert.Equal(640m, draft.TotalAmount); // (2*280) + (2*40)
    }

    [Fact]
    public async Task SubmitOrderAsync_ValidVersion_TransitionsToSubmitted()
    {
        var tableId = Guid.NewGuid();
        var draft = await _store.CreateOrUpdateTableDraftAsync(new CreateTableDraftRequest(
            tableId,
            "M-01",
            "Garson Ahmet",
            new List<OrderItemDraftDto> { new(Guid.NewGuid(), "Çorba", 1, 90m) }
        ));

        var submitted = await _store.SubmitOrderAsync(draft.OrderId, draft.RowVersion);

        Assert.Equal("Submitted", submitted.Status);
        Assert.Equal(draft.RowVersion + 1, submitted.RowVersion);
    }

    [Fact]
    public async Task SubmitOrderAsync_StaleVersion_ThrowsInvalidOperationException()
    {
        var tableId = Guid.NewGuid();
        var draft = await _store.CreateOrUpdateTableDraftAsync(new CreateTableDraftRequest(
            tableId,
            "M-01",
            "Garson Ahmet",
            new List<OrderItemDraftDto> { new(Guid.NewGuid(), "Çorba", 1, 90m) }
        ));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _store.SubmitOrderAsync(draft.OrderId, expectedRowVersion: 999));
    }
}
