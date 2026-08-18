using FluentAssertions;
using Xunit;

namespace ALKAROS.Clients.WaiterPwa.OrderEntry.Tests;

public sealed class WaiterOrderEntryTests
{
    private readonly WaiterOrderEntryEngine _engine = new();

    [Fact]
    public void TableSelectionAndDraftTotalsCalculateAccurately()
    {
        var table = new WaiterTableOption(Guid.NewGuid(), "M-04", "Available", ExpectedRowVersion: 1);
        _engine.SelectTable(table);

        string[] testModifiers = ["Limon", "Kruton"];
        _engine.AddItem(Guid.NewGuid(), "Mercimek Çorbası", 60.00m, quantity: 2, modifiers: testModifiers);
        _engine.AddItem(Guid.NewGuid(), "Kola", 35.00m, quantity: 1);
        _engine.SetNote("Çorbalar sıcak gelsin");

        var draft = _engine.CurrentDraft;
        draft.Should().NotBeNull();
        draft!.TableNumber.Should().Be("M-04");
        draft.TotalItemCount.Should().Be(3);
        draft.TotalAmount.Should().Be(155.00m); // (60 * 2) + 35 = 155.00
        draft.OrderNote.Should().Be("Çorbalar sıcak gelsin");
    }

    [Fact]
    public void ReconnectOrDoubleSubmitReusesSameIdempotencyKey()
    {
        var table = new WaiterTableOption(Guid.NewGuid(), "B-02", "Available", 1);
        _engine.SelectTable(table);
        _engine.AddItem(Guid.NewGuid(), "Su", 10.00m, 2);

        var key1 = _engine.BeginSubmission();
        var key2 = _engine.BeginSubmission();

        key2.Should().Be(key1); // Idempotency key reused during transmission
    }

    [Fact]
    public void StaleTableConflictSurfacesExplicitErrorAndPreservesDraftWithoutSilentRelocation()
    {
        var table = new WaiterTableOption(Guid.NewGuid(), "T-05", "Available", 1);
        _engine.SelectTable(table);
        _engine.AddItem(Guid.NewGuid(), "Lahmacun", 80.00m, 3);

        _engine.BeginSubmission();

        // Server reports table conflict (Acceptance Evidence #2)
        _engine.HandleTableConflict(serverTableVersion: 2);

        _engine.IsSubmitting.Should().BeFalse();
        _engine.ErrorMessage.Should().Contain("durumu değişti");
        _engine.ErrorMessage.Should().Contain("Sipariş sessizce taşınmadı");

        // Draft is preserved
        _engine.CurrentDraft.Should().NotBeNull();
        _engine.CurrentDraft!.Items.Should().ContainSingle();
        _engine.CurrentDraft.Items[0].ProductName.Should().Be("Lahmacun");
    }

    [Fact]
    public void SuccessClearsDraft()
    {
        var table = new WaiterTableOption(Guid.NewGuid(), "M-01", "Available", 1);
        _engine.SelectTable(table);
        _engine.AddItem(Guid.NewGuid(), "Tatlı", 90.00m, 1);

        _engine.BeginSubmission();
        _engine.HandleSubmissionSuccess(Guid.NewGuid());

        _engine.IsSubmitting.Should().BeFalse();
        _engine.CurrentDraft!.Items.Should().BeEmpty();
    }
}
