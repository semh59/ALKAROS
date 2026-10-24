namespace ALKAROS.Orders.OrderAggregate.Tests;

using ALKAROS.Orders.OrderAggregate;
using FluentAssertions;
using Xunit;

public class OrderMoneyAndCourseTests
{
    private static OrderItem Item(
        decimal unitPrice = 100m, decimal quantity = 1, decimal taxRate = 10m, decimal discount = 0m,
        OrderItemState status = OrderItemState.Draft, int? course = null)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Lahmacun", quantity, unitPrice, taxRate,
            discountAmount: discount, status: status, courseNumber: course);

    private static Order OrderOf(params OrderItem[] items)
        => new(Guid.NewGuid(), OrderSource.Waiter, "ORD-2001", items);

    [Fact]
    public void ALineEqualToItsDiscountIsFreeAndOneKurusMoreIsRefused()
    {
        Item(unitPrice: 50m, discount: 50m).GrossAmount.Should().Be(0m);

        var tooMuch = () => Item(unitPrice: 50m, discount: 50.01m);
        tooMuch.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HalfAKurusRoundsAwayFromZeroInTheLineAndInTheTax()
    {
        // 0.5 x 0.05 = 0.025 and, at a 100% rate, 0.09 / 2 = 0.045: banker's rounding would give 0.02 and 0.04.
        Item(unitPrice: 0.05m, quantity: 0.5m).GrossAmount.Should().Be(0.03m);
        Item(unitPrice: 0.09m, taxRate: 100m).TaxAmount.Should().Be(0.05m);
    }

    [Fact]
    public void ACompItemKeepsItsPriceAndDiscountInEveryOrderTotalButIsNotActive()
    {
        var comp = Item(unitPrice: 80m, discount: 80m, status: OrderItemState.Complimentary);
        var order = OrderOf(Item(unitPrice: 100m, status: OrderItemState.Active), comp);

        comp.IsActive.Should().BeFalse();
        order.Subtotal.Should().Be(180m);
        order.DiscountTotal.Should().Be(80m);
        order.Total.Should().Be(100m);
        order.TaxTotal.Should().Be(9.09m);
    }

    [Fact]
    public void ACancelledItemCountsInNoTotal()
    {
        var order = OrderOf(Item(unitPrice: 100m, status: OrderItemState.Active), Item(unitPrice: 500m, status: OrderItemState.Cancelled));

        order.Subtotal.Should().Be(100m);
        order.Total.Should().Be(100m);
    }

    [Fact]
    public void OnlyTheLowestCourseOfARoundGoesToTheKitchenAndTheOthersWait()
    {
        var third = Item(course: 3);
        var first = Item(course: 1);
        var second = Item(course: 2);
        var plain = Item();

        var (_, fired) = OrderOf(third, first, second, plain).FireRound();

        fired.Single(i => i.Id == first.Id).KitchenState.Should().Be(KitchenState.Sent);
        fired.Single(i => i.Id == second.Id).KitchenState.Should().Be(KitchenState.Held);
        fired.Single(i => i.Id == third.Id).KitchenState.Should().Be(KitchenState.Held);
        fired.Single(i => i.Id == plain.Id).KitchenState.Should().Be(KitchenState.Sent);
    }

    [Fact]
    public void ALaterRoundWithOnlyAHigherCourseSendsItImmediately()
    {
        var alreadySent = Item(course: 1, status: OrderItemState.Active);
        var later = Item(course: 2);

        var (_, fired) = OrderOf(alreadySent, later).FireRound();

        fired.Should().ContainSingle().Which.KitchenState.Should().Be(KitchenState.Sent);
    }
}
