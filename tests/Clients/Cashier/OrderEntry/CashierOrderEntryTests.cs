using FluentAssertions;
using Xunit;

namespace ALKAROS.Clients.Cashier.OrderEntry.Tests;

public sealed class CashierOrderEntryTests
{
    private readonly Guid _tableId = Guid.NewGuid();
    private readonly string _tableNumber = "M-12";
    private readonly OrderEntryEngine _engine;

    public CashierOrderEntryTests()
    {
        _engine = new OrderEntryEngine(_tableId, _tableNumber);
    }

    [Fact]
    public void AddingProductsAndModifiersCalculatesSubtotalAccurately()
    {
        var burger = new CatalogProductItem(
            ProductId: Guid.NewGuid(),
            Name: "Alkaros Burger",
            Category: "Ana Yemek",
            BasePrice: 120.00m,
            Modifiers: new List<CatalogModifierItem>
            {
                new(Guid.NewGuid(), "Ekstra Peynir", 15.00m)
            });

        var ayran = new CatalogProductItem(
            ProductId: Guid.NewGuid(),
            Name: "Köy Ayranı",
            Category: "İçecek",
            BasePrice: 25.00m,
            Modifiers: Array.Empty<CatalogModifierItem>());

        // 1. Add 2 Burgers with Extra Cheese ( (120 + 15) * 2 = 270.00 TL )
        _engine.AddItem(burger, quantity: 2, modifiers: new[]
        {
            new SelectedModifier(burger.Modifiers[0].ModifierId, "Ekstra Peynir", 15.00m)
        }, specialInstructions: "Az pişmiş");

        // 2. Add 1 Ayran ( 25.00 TL )
        _engine.AddItem(ayran, quantity: 1);

        var draft = _engine.CurrentDraft;

        draft.TableNumber.Should().Be("M-12");
        draft.TotalItemCount.Should().Be(3);
        draft.Items.Should().HaveCount(2);

        // Line 1: (120 + 15) * 2 = 270.00
        draft.Items[0].LineTotal.Should().Be(270.00m);
        // Line 2: 25.00 * 1 = 25.00
        draft.Items[1].LineTotal.Should().Be(25.00m);

        // Subtotal = 295.00 TL
        draft.Subtotal.Should().Be(295.00m);
    }

    [Fact]
    public void DoubleClickOrRetryReusesSameIdempotencyKey()
    {
        var product = new CatalogProductItem(Guid.NewGuid(), "Çay", "İçecek", 15.00m, Array.Empty<CatalogModifierItem>());
        _engine.AddItem(product, quantity: 1);

        // First click
        var key1 = _engine.BeginSubmission();
        _engine.IsSubmitting.Should().BeTrue();

        // Immediate second click (e.g. fast double click by cashier or network retry) (Acceptance Evidence #1)
        var key2 = _engine.BeginSubmission();

        key2.Should().Be(key1); // Exactly identical idempotency key
    }

    [Fact]
    public void SubmissionFailurePreservesRecoverableDraft()
    {
        var product = new CatalogProductItem(Guid.NewGuid(), "Kebap", "Ana Yemek", 250.00m, Array.Empty<CatalogModifierItem>());
        _engine.AddItem(product, quantity: 1, specialInstructions: "Acılı");
        _engine.SetNote("Masa VIP");

        var key = _engine.BeginSubmission();

        // Simulate server rejection (e.g. price mismatch or validation error) (Acceptance Evidence #2)
        _engine.HandleSubmissionFailure("Sunucu fiyat uyuşmazlığı tespit etti.");

        _engine.IsSubmitting.Should().BeFalse();

        // Draft is preserved for cashier recovery
        var draft = _engine.CurrentDraft;
        draft.Items.Should().ContainSingle();
        draft.Items[0].ProductName.Should().Be("Kebap");
        draft.Note.Should().Be("Masa VIP");
    }

    [Fact]
    public void QuantityUpdateAndRemovalOperatesAccurately()
    {
        var product = new CatalogProductItem(Guid.NewGuid(), "Kahve", "İçecek", 40.00m, Array.Empty<CatalogModifierItem>());
        _engine.AddItem(product, quantity: 2);

        var itemId = _engine.CurrentDraft.Items[0].ItemId;

        // Update quantity to 4
        _engine.UpdateQuantity(itemId, 4);
        _engine.CurrentDraft.Items[0].Quantity.Should().Be(4);
        _engine.CurrentDraft.Subtotal.Should().Be(160.00m);

        // Update quantity to 0 -> Removes item
        _engine.UpdateQuantity(itemId, 0);
        _engine.CurrentDraft.Items.Should().BeEmpty();
        _engine.CurrentDraft.Subtotal.Should().Be(0.00m);
    }
}
