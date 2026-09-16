namespace ALKAROS.Orders.OrderAggregate.Tests;

using ALKAROS.Orders.OrderAggregate;
using FluentAssertions;
using Xunit;

public class OrderDomainTests
{
    private static OrderItem NewItem(OrderItemState state = OrderItemState.Draft, decimal unitPrice = 100m, decimal quantity = 1)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Lahmacun", quantity, unitPrice, 10m, status: state);

    private static Order NewOrder(OrderState state = OrderState.Draft, params OrderItem[] items)
        => new(
            Guid.NewGuid(),
            OrderSource.Waiter,
            "ORD-1001",
            items.Length == 0 ? new[] { NewItem() } : items,
            status: state);

    [Theory]
    [InlineData(OrderState.Submitted, true)]
    [InlineData(OrderState.PendingConfirmation, false)]
    [InlineData(OrderState.Accepted, false)]
    [InlineData(OrderState.Rejected, false)]
    [InlineData(OrderState.Preparing, false)]
    [InlineData(OrderState.Ready, false)]
    [InlineData(OrderState.Served, false)]
    [InlineData(OrderState.Completed, false)]
    [InlineData(OrderState.Cancelled, true)]
    [InlineData(OrderState.Draft, false)]
    public void DraftCanTransitionTo(OrderState target, bool allowed)
        => NewOrder(OrderState.Draft).CanTransitionTo(target).Should().Be(allowed);

    [Theory]
    [InlineData(OrderState.PendingConfirmation, true)]
    [InlineData(OrderState.Submitted, false)]
    [InlineData(OrderState.Accepted, false)]
    [InlineData(OrderState.Preparing, false)]
    [InlineData(OrderState.Cancelled, true)]
    [InlineData(OrderState.Served, false)]
    public void SubmittedCanTransitionTo(OrderState target, bool allowed)
        => NewOrder(OrderState.Submitted).CanTransitionTo(target).Should().Be(allowed);

    [Theory]
    [InlineData(OrderState.Accepted, true)]
    [InlineData(OrderState.Rejected, true)]
    [InlineData(OrderState.Preparing, false)]
    [InlineData(OrderState.Submitted, false)]
    [InlineData(OrderState.Cancelled, true)]
    [InlineData(OrderState.Draft, false)]
    public void PendingConfirmationCanTransitionTo(OrderState target, bool allowed)
        => NewOrder(OrderState.PendingConfirmation).CanTransitionTo(target).Should().Be(allowed);

    [Theory]
    [InlineData(OrderState.Preparing, true)]
    [InlineData(OrderState.Accepted, false)]
    [InlineData(OrderState.Ready, false)]
    [InlineData(OrderState.Completed, false)]
    [InlineData(OrderState.Cancelled, true)]
    public void AcceptedCanTransitionTo(OrderState target, bool allowed)
        => NewOrder(OrderState.Accepted).CanTransitionTo(target).Should().Be(allowed);

    [Theory]
    [InlineData(OrderState.Ready, true)]
    [InlineData(OrderState.Preparing, false)]
    [InlineData(OrderState.Served, false)]
    [InlineData(OrderState.Accepted, false)]
    [InlineData(OrderState.Cancelled, true)]
    public void PreparingCanTransitionTo(OrderState target, bool allowed)
        => NewOrder(OrderState.Preparing).CanTransitionTo(target).Should().Be(allowed);

    [Theory]
    [InlineData(OrderState.Served, true)]
    [InlineData(OrderState.Preparing, false)]
    [InlineData(OrderState.Ready, false)]
    [InlineData(OrderState.Completed, false)]
    [InlineData(OrderState.Cancelled, true)]
    public void ReadyCanTransitionTo(OrderState target, bool allowed)
        => NewOrder(OrderState.Ready).CanTransitionTo(target).Should().Be(allowed);

    [Theory]
    [InlineData(OrderState.Completed, true)]
    [InlineData(OrderState.Served, false)]
    [InlineData(OrderState.Accepted, false)]
    [InlineData(OrderState.Cancelled, false)]
    [InlineData(OrderState.Ready, false)]
    public void ServedCanTransitionTo(OrderState target, bool allowed)
        => NewOrder(OrderState.Served).CanTransitionTo(target).Should().Be(allowed);

    [Fact]
    public void TransitionToAllowedUpdatesStateAndStampsTimestamp()
    {
        var order = NewOrder(OrderState.Draft);

        var submitted = order.TransitionTo(OrderState.Submitted, changedBy: Guid.NewGuid());

        submitted.Status.Should().Be(OrderState.Submitted);
        submitted.SubmittedAt.Should().NotBeNull();
        submitted.Id.Should().Be(order.Id);
        submitted.RowVersion.Should().Be(order.RowVersion);
        submitted.History.Should().HaveCount(1);
        submitted.History[0].OldStatus.Should().Be(OrderState.Draft);
        submitted.History[0].NewStatus.Should().Be(OrderState.Submitted);
    }

    [Fact]
    public void ForbiddenServedToAcceptedThrows()
    {
        var order = NewOrder(OrderState.Served);

        var act = () => order.TransitionTo(OrderState.Accepted);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Order {order.Id} cannot transition from Served to Accepted.");
    }

    [Fact]
    public void CompletedCannotTransitionToPreparing()
    {
        var order = NewOrder(OrderState.Completed);

        var act = () => order.TransitionTo(OrderState.Preparing);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Order {order.Id} cannot transition from Completed to Preparing.");
    }

    [Fact]
    public void DraftCannotSkipToAccepted()
    {
        var order = NewOrder(OrderState.Draft);

        var act = () => order.TransitionTo(OrderState.Accepted);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CancelledCannotReopenToAccepted()
    {
        var order = NewOrder(OrderState.Cancelled);

        var act = () => order.TransitionTo(OrderState.Accepted);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void PendingConfirmationSetsConfirmationStatusPending()
    {
        var order = NewOrder(OrderState.Submitted);

        var pending = order.TransitionTo(OrderState.PendingConfirmation);

        pending.ConfirmationStatus.Should().Be(ConfirmationStatus.Pending);
    }

    [Fact]
    public void AcceptedSetsConfirmationStatusAccepted()
    {
        var order = NewOrder(OrderState.PendingConfirmation);

        var accepted = order.TransitionTo(OrderState.Accepted);

        accepted.ConfirmationStatus.Should().Be(ConfirmationStatus.Accepted);
        accepted.AcceptedAt.Should().NotBeNull();
    }

    [Fact]
    public void RejectedSetsConfirmationStatusRejected()
    {
        var order = NewOrder(OrderState.PendingConfirmation);

        var rejected = order.TransitionTo(OrderState.Rejected);

        rejected.ConfirmationStatus.Should().Be(ConfirmationStatus.Rejected);
    }

    [Fact]
    public void CompletedStampsClosedAt()
    {
        var order = NewOrder(OrderState.Served);

        var completed = order.TransitionTo(OrderState.Completed);

        completed.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public void CancelledStampsCancelledAt()
    {
        var order = NewOrder(OrderState.Preparing);

        var cancelled = order.TransitionTo(OrderState.Cancelled, reason: "no stock");

        cancelled.CancelledAt.Should().NotBeNull();
        cancelled.History.Should().HaveCount(1);
        cancelled.History[0].Reason.Should().Be("no stock");
    }

    [Fact]
    public void EmptyOrderNumberIsRejected()
    {
        var act = () => new Order(Guid.NewGuid(), OrderSource.Waiter, "   ", new[] { NewItem() });

        act.Should().Throw<ArgumentException>().WithParameterName("orderNumber");
    }

    [Fact]
    public void OrderDefaultsMatchPdfSchema()
    {
        var order = NewOrder();

        order.Status.Should().Be(OrderState.Draft);
        order.ConfirmationStatus.Should().Be(ConfirmationStatus.NotRequired);
        order.CurrencyCode.Should().Be("TRY");
        order.Subtotal.Should().Be(100m);
        order.DiscountTotal.Should().Be(0m);
        order.TaxTotal.Should().Be(10m);
        order.Total.Should().Be(110m);
        order.SubmittedAt.Should().BeNull();
        order.AcceptedAt.Should().BeNull();
        order.RowVersion.Should().Be(1);
    }

    [Fact]
    public void AddItemIsRejectedAfterSubmission()
    {
        var order = NewOrder(OrderState.Submitted);

        var act = () => order.AddItem(NewItem());

        act.Should().Throw<InvalidOperationException>();
    }
}

/// <summary>
/// V1-RMD-120: Order/OrderItem's own line-quantity-change and line-removal
/// operations, added so Host/DualScreen's cart could stop re-implementing
/// them in raw SQL outside the aggregate (an independent audit, 2026-09-07,
/// found this channel's writes to orders.orders/order_items never touched
/// Order/OrderItem at all).
/// </summary>
public class OrderItemQuantityAndRemovalTests
{
    private static OrderItem NewDraftItem(Guid? id = null, decimal unitPrice = 100m, decimal quantity = 1)
        => new(id ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Lahmacun", quantity, unitPrice, 10m);

    [Fact]
    public void ChangeQuantityRecomputesNetTaxAndGrossFromScratch()
    {
        var item = NewDraftItem(unitPrice: 50m, quantity: 1);

        var changed = item.ChangeQuantity(3);

        changed.Quantity.Should().Be(3);
        changed.NetAmount.Should().Be(150m);
        changed.TaxAmount.Should().Be(15m);
        changed.GrossAmount.Should().Be(165m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ChangeQuantityRejectsNonPositiveQuantity(decimal quantity)
    {
        var item = NewDraftItem();

        var act = () => item.ChangeQuantity(quantity);

        act.Should().Throw<ArgumentException>().WithParameterName(nameof(quantity));
    }

    [Fact]
    public void ChangeQuantityRejectsANonDraftItem()
    {
        var item = new OrderItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Lahmacun", 1, 100m, 10m,
            status: OrderItemState.Active);

        var act = () => item.ChangeQuantity(2);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OrderChangeItemQuantityUpdatesTheMatchingItemAndRecomputesOrderTotals()
    {
        var itemId = Guid.NewGuid();
        var order = new Order(Guid.NewGuid(), OrderSource.Cashier, "ORD-5001", [NewDraftItem(itemId, unitPrice: 20m, quantity: 1)]);

        var updated = order.ChangeItemQuantity(itemId, 5);

        updated.Items.Single().Quantity.Should().Be(5);
        updated.Subtotal.Should().Be(100m);
        updated.Total.Should().Be(110m);
    }

    [Fact]
    public void OrderChangeItemQuantityRejectsAfterSubmission()
    {
        var itemId = Guid.NewGuid();
        var order = new Order(Guid.NewGuid(), OrderSource.Cashier, "ORD-5002", [NewDraftItem(itemId)]).Submit();

        var act = () => order.ChangeItemQuantity(itemId, 2);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OrderChangeItemQuantityRejectsAnUnknownItem()
    {
        var order = new Order(Guid.NewGuid(), OrderSource.Cashier, "ORD-5003", [NewDraftItem()]);

        var act = () => order.ChangeItemQuantity(Guid.NewGuid(), 2);

        act.Should().Throw<ArgumentException>().WithParameterName("orderItemId");
    }

    [Fact]
    public void OrderRemoveItemDropsTheItemAndRecomputesOrderTotals()
    {
        var keepId = Guid.NewGuid();
        var removeId = Guid.NewGuid();
        var order = new Order(
            Guid.NewGuid(), OrderSource.Cashier, "ORD-5004",
            [NewDraftItem(keepId, unitPrice: 30m), NewDraftItem(removeId, unitPrice: 70m)]);

        var updated = order.RemoveItem(removeId);

        updated.Items.Should().ContainSingle(i => i.Id == keepId);
        updated.Subtotal.Should().Be(30m);
    }

    [Fact]
    public void OrderRemoveItemRejectsAfterSubmission()
    {
        var itemId = Guid.NewGuid();
        var order = new Order(Guid.NewGuid(), OrderSource.Cashier, "ORD-5005", [NewDraftItem(itemId)]).Submit();

        var act = () => order.RemoveItem(itemId);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OrderRemoveItemRejectsAnAlreadyActiveItem()
    {
        // Draft order with one Active item cannot happen through Submit (which
        // activates every item at once), but AdvanceItemKitchenState/CancelItem
        // never touch Status back to Draft either — this guards RemoveItem's
        // own invariant directly regardless of how such a state is reached.
        var itemId = Guid.NewGuid();
        var activeItem = new OrderItem(
            itemId, Guid.NewGuid(), Guid.NewGuid(), "Lahmacun", 1, 100m, 10m, status: OrderItemState.Active);
        var order = new Order(Guid.NewGuid(), OrderSource.Cashier, "ORD-5006", [activeItem]);

        var act = () => order.RemoveItem(itemId);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OrderRemoveItemRejectsAnUnknownItem()
    {
        var order = new Order(Guid.NewGuid(), OrderSource.Cashier, "ORD-5007", [NewDraftItem()]);

        var act = () => order.RemoveItem(Guid.NewGuid());

        act.Should().Throw<ArgumentException>().WithParameterName("orderItemId");
    }
}

public class OrderItemStateTests
{
    private static OrderItem NewItem(OrderItemState state, KitchenState kitchenState)
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ayran",
            1,
            20m,
            10m,
            status: state,
            kitchenState: kitchenState);

    [Fact]
    public void DiscountCannotExceedLineSubtotal()
    {
        var act = () => new OrderItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ayran",
            1,
            100m,
            10m,
            discountAmount: 150m);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot exceed the order item subtotal*");
    }

    [Fact]
    public void DraftItemActivatesOnSubmit()
    {
        var item = NewItem(OrderItemState.Draft, KitchenState.NotSent);

        var active = item.Activate();

        active.Status.Should().Be(OrderItemState.Active);
        active.KitchenState.Should().Be(KitchenState.NotSent);
    }

    [Fact]
    public void NonDraftItemCannotActivate()
    {
        var item = NewItem(OrderItemState.Active, KitchenState.NotSent);

        var act = () => item.Activate();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void NotPreparedActiveItemCanBeVoided()
    {
        var item = NewItem(OrderItemState.Active, KitchenState.NotSent);

        var cancelled = item.Cancel();

        cancelled.Status.Should().Be(OrderItemState.Cancelled);
        cancelled.KitchenState.Should().Be(KitchenState.Cancelled);
    }

    [Theory]
    [InlineData(KitchenState.Sent)]
    [InlineData(KitchenState.Preparing)]
    [InlineData(KitchenState.Ready)]
    public void SentButUnservedActiveItemCanBeVoided(KitchenState kitchenState)
    {
        // V0-DOM-006 amendment (2026-09-04, V1-IAM-027): sent-but-unserved is
        // no longer a hard wall at the domain level — it is gated by the
        // bills.void grant one layer up (Host.Experience), not here.
        var item = NewItem(OrderItemState.Active, kitchenState);

        var cancelled = item.Cancel();

        cancelled.Status.Should().Be(OrderItemState.Cancelled);
        cancelled.KitchenState.Should().Be(KitchenState.Cancelled);
    }

    [Fact]
    public void ServedActiveItemCannotBeVoided()
    {
        // Served stays a hard wall: that is comp/refund territory, never void.
        var item = NewItem(OrderItemState.Active, KitchenState.Served);

        var act = () => item.Cancel();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Order item {item.Id} cannot be voided once it reached kitchen state Served.");
    }

    [Fact]
    public void AlreadyCancelledItemCannotBeVoided()
    {
        var item = NewItem(OrderItemState.Cancelled, KitchenState.Cancelled);

        var act = () => item.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SnapshotValuesAreFrozenAtConstruction()
    {
        var item = new OrderItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Kebap",
            2,
            250m,
            10m,
            skuSnapshot: "K-01");

        item.ProductNameSnapshot.Should().Be("Kebap");
        item.SkuSnapshot.Should().Be("K-01");
        item.UnitPrice.Should().Be(250m);

        item.LineSubtotalValue.Should().Be(500m);
        item.NetAmount.Should().Be(500m);
        item.TaxAmount.Should().Be(50m);
        item.GrossAmount.Should().Be(550m);
    }

    [Fact]
    public void ModifiersParticipateInLineSubtotal()
    {
        var modifier = new OrderItemModifier(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Extra Cheese", 15m);
        var item = new OrderItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Pizza",
            1,
            200m,
            10m,
            modifiers: [modifier]);

        item.LineSubtotalValue.Should().Be(215m);
        item.NetAmount.Should().Be(215m);
        item.TaxAmount.Should().Be(21.5m);
        item.GrossAmount.Should().Be(236.5m);
    }

    [Fact]
    public void DiscountReducesNetBeforeTax()
    {
        var item = new OrderItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Martı",
            1,
            100m,
            10m,
            discountAmount: 10m);

        item.NetAmount.Should().Be(90m);
        item.TaxAmount.Should().Be(9m);
        item.GrossAmount.Should().Be(99m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveQuantityIsRejected(decimal quantity)
    {
        var act = () => new OrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "X", quantity, 10m, 10m);

        act.Should().Throw<ArgumentException>().WithParameterName(nameof(quantity));
    }
}

public class OrderSubmitTests
{
    [Fact]
    public void DraftOrderSubmitsAndActivatesItems()
    {
        var draftItem = new OrderItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Köfte", 1, 150m, 10m);
        var order = new Order(Guid.NewGuid(), OrderSource.Cashier, "ORD-2001", [draftItem]);

        var submitted = order.Submit(changedBy: Guid.NewGuid());

        submitted.Status.Should().Be(OrderState.Submitted);
        submitted.Items.Single().Status.Should().Be(OrderItemState.Active);
        submitted.History.Single().NewStatus.Should().Be(OrderState.Submitted);
    }

    [Fact]
    public void SubmittedOrderCannotBeSubmittedAgain()
    {
        var order = NewSubmittedOrder();

        var act = () => order.Submit();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EmptyOrderCannotBeSubmitted()
    {
        var order = new Order(Guid.NewGuid(), OrderSource.Qr, "ORD-2002", Array.Empty<OrderItem>());

        var act = () => order.Submit();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Order {order.Id} has no items to submit.");
    }

    [Fact]
    public void CancelledItemCannotSupportSubmission()
    {
        var cancelled = new OrderItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Süt", 1, 30m, 10m,
            status: OrderItemState.Cancelled, kitchenState: KitchenState.Cancelled);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-2003", [cancelled]);

        var act = () => order.Submit();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Order {order.Id} has no items to submit.");
    }

    private static Order NewSubmittedOrder()
    {
        var draft = new OrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Tost", 1, 100m, 10m);
        return new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-2004", [draft]).Submit();
    }
}

/// <summary>
/// V1-WTR-025: full course model — FireRound splits a round with a course
/// structure into one immediately-Sent course and the rest Held; FireCourse
/// calls in a Held course explicitly.
/// </summary>
public class OrderCourseTests
{
    private static OrderItem CourseItem(int courseNumber, string name = "Çorba")
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), name, 1, 60m, 10m, courseNumber: courseNumber);

    [Fact]
    public void FiringAMultiCourseRoundSendsOnlyTheLowestCourseAndHoldsTheRest()
    {
        var starter = CourseItem(1, "Çorba");
        var main = CourseItem(2, "Izgara");
        var dessert = CourseItem(3, "Baklava");
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-4001", [starter, main, dessert]);

        var (fired, firedItems) = order.FireRound();

        firedItems.Should().HaveCount(3);
        fired.Items.Single(i => i.Id == starter.Id).KitchenState.Should().Be(KitchenState.Sent);
        fired.Items.Single(i => i.Id == main.Id).KitchenState.Should().Be(KitchenState.Held);
        fired.Items.Single(i => i.Id == dessert.Id).KitchenState.Should().Be(KitchenState.Held);
        // All three still activate together — the whole plan is committed
        // to the check at once, only the kitchen state differs per course.
        fired.Items.Should().OnlyContain(i => i.Status == OrderItemState.Active);
        fired.Status.Should().Be(OrderState.Submitted);
    }

    [Fact]
    public void AnItemWithNoCourseNumberFiresSentImmediatelyEvenAlongsideCourses()
    {
        var noCourse = new OrderItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Su", 1, 10m, 10m);
        var mainCourse = CourseItem(1, "Pilav");
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-4002", [noCourse, mainCourse]);

        var (fired, _) = order.FireRound();

        fired.Items.Single(i => i.Id == noCourse.Id).KitchenState.Should().Be(KitchenState.Sent);
    }

    [Fact]
    public void FireCoursePromotesOnlyThatCoursesHeldItemsToSent()
    {
        var starter = CourseItem(1, "Çorba");
        var main = CourseItem(2, "Izgara");
        var dessert = CourseItem(3, "Baklava");
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-4003", [starter, main, dessert])
            .FireRound().Order;

        var (fired, firedItems) = order.FireCourse(2, changedBy: Guid.NewGuid());

        firedItems.Should().ContainSingle(i => i.Id == main.Id);
        fired.Items.Single(i => i.Id == main.Id).KitchenState.Should().Be(KitchenState.Sent);
        // Course 3 is untouched — a separate, later fire is required for it.
        fired.Items.Single(i => i.Id == dessert.Id).KitchenState.Should().Be(KitchenState.Held);
    }

    [Fact]
    public void FiringACourseWithNoHeldItemsThrows()
    {
        var starter = CourseItem(1, "Çorba");
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-4004", [starter])
            .FireRound().Order;

        // Course 1 already fired Sent (it was the lowest in the round); no
        // Held items exist for course 2 at all.
        var act = () => order.FireCourse(2);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Order {order.Id} has no held items for course 2 to fire.");
    }

    [Fact]
    public void FiringTheSameCourseTwiceThrowsTheSecondTime()
    {
        var starter = CourseItem(1);
        var main = CourseItem(2);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-4005", [starter, main])
            .FireRound().Order;

        var afterFirstFire = order.FireCourse(2).Order;
        var act = () => afterFirstFire.FireCourse(2);

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// V1-RMD-182: found by the 2026-09-12 five-agent independent Garson
    /// audit — FireCourse had no IsOpenCheck guard, unlike FireRound and
    /// every other state-changing method on this aggregate. A Cancelled
    /// order can still carry Held items (a later course of an already-fired
    /// round, never called in before the check was voided); without the
    /// guard, calling FireCourse on it would still promote them to Sent and
    /// hand back items for the caller to dispatch a fresh kitchen ticket
    /// for - cooking a course of an order nobody is paying for anymore.
    /// </summary>
    [Fact]
    public void FiringACourseOnACancelledOrderThrows()
    {
        var starter = CourseItem(1);
        var main = CourseItem(2);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-4006", [starter, main])
            .FireRound().Order
            .TransitionTo(OrderState.Cancelled);

        var act = () => order.FireCourse(2);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Order {order.Id} cannot fire a course from Cancelled.");
    }

    /// <summary>
    /// V1-RMD-217: found by an independent audit (2026-09-16) - if
    /// GarsonFeature.CourseManagement is disabled mid-service (or any other
    /// caller simply never calls FireCourse), a Held course used to be able
    /// to ride the check all the way to Served/Completed untouched: nobody
    /// ever validated that every item had actually left KitchenState.Held
    /// before the order could be marked served. The customer's dessert
    /// (say) would never reach the kitchen, and once Served, FireCourse
    /// could no longer even be called (IsOpenCheck already refuses it).
    /// </summary>
    [Fact]
    public void CannotBeMarkedServedWhileACourseIsStillHeld()
    {
        var starter = CourseItem(1);
        var dessert = CourseItem(2);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-4007", [starter, dessert])
            .FireRound().Order
            .TransitionTo(OrderState.PendingConfirmation)
            .TransitionTo(OrderState.Accepted)
            .TransitionTo(OrderState.Preparing)
            .TransitionTo(OrderState.Ready);

        order.CanTransitionTo(OrderState.Served).Should().BeFalse();
        var act = () => order.TransitionTo(OrderState.Served);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CanBeMarkedServedOnceEveryCourseHasBeenFired()
    {
        var starter = CourseItem(1);
        var dessert = CourseItem(2);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-4008", [starter, dessert])
            .FireRound().Order
            .FireCourse(2).Order
            .TransitionTo(OrderState.PendingConfirmation)
            .TransitionTo(OrderState.Accepted)
            .TransitionTo(OrderState.Preparing)
            .TransitionTo(OrderState.Ready);

        order.CanTransitionTo(OrderState.Served).Should().BeTrue();
    }
}

public class OrderVoidTests
{
    [Fact]
    public void VoidEligibleItemCancelledInDraftOrder()
    {
        var itemId = Guid.NewGuid();
        var item = new OrderItem(
            itemId, Guid.NewGuid(), Guid.NewGuid(), "Kazandibi", 1, 40m, 10m,
            status: OrderItemState.Active, kitchenState: KitchenState.NotSent);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-3001", [item]);

        var cancelled = order.CancelItem(itemId, reason: "customer changed mind");

        cancelled.Items.Single().Status.Should().Be(OrderItemState.Cancelled);
    }

    [Fact]
    public void CancelItemAppendsAHistoryEntryRecordingReasonActorAndTimestamp()
    {
        // Regression test for an independent audit finding (2026-09-05):
        // reason/changedBy/changedAt were accepted by CancelItem's
        // signature but silently dropped — no OrderStatusHistoryEntry was
        // ever appended for an item-level void, unlike every order-level
        // TransitionTo.
        var itemId = Guid.NewGuid();
        var item = new OrderItem(
            itemId, Guid.NewGuid(), Guid.NewGuid(), "Kazandibi", 1, 40m, 10m,
            status: OrderItemState.Active, kitchenState: KitchenState.NotSent);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-3001B", [item]);
        var actorId = Guid.NewGuid();
        var changedAt = DateTimeOffset.UtcNow;

        var cancelled = order.CancelItem(itemId, reason: "customer changed mind", changedBy: actorId, changedAt: changedAt);

        var entry = cancelled.History.Should().ContainSingle().Subject;
        entry.Reason.Should().Be("customer changed mind");
        entry.ChangedBy.Should().Be(actorId);
        entry.ChangedAt.Should().Be(changedAt);
        entry.OldStatus.Should().Be(order.Status);
        entry.NewStatus.Should().Be(order.Status);
    }

    [Fact]
    public void SentButUnservedItemCanNowBeVoidedAtTheOrderLevel()
    {
        // V0-DOM-006 amendment (2026-09-04, V1-IAM-027): the domain no
        // longer walls this off — it is gated by the bills.void grant one
        // layer up (Host.Experience.Orders.SentItemVoid).
        var itemId = Guid.NewGuid();
        var item = new OrderItem(
            itemId, Guid.NewGuid(), Guid.NewGuid(), "Hamburger", 1, 80m, 10m,
            status: OrderItemState.Active, kitchenState: KitchenState.Preparing);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-3002", [item], status: OrderState.Preparing);

        var cancelled = order.CancelItem(itemId);

        cancelled.Items.Single().Status.Should().Be(OrderItemState.Cancelled);
        cancelled.Items.Single().KitchenState.Should().Be(KitchenState.Cancelled);
    }

    [Fact]
    public void VoidOnATerminalOrderThrows()
    {
        var itemId = Guid.NewGuid();
        var item = new OrderItem(
            itemId, Guid.NewGuid(), Guid.NewGuid(), "Hamburger", 1, 80m, 10m,
            status: OrderItemState.Active, kitchenState: KitchenState.Served);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-3002B", [item], status: OrderState.Served)
            .TransitionTo(OrderState.Completed);

        var act = () => order.CancelItem(itemId);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UnknownItemCannotBeVoided()
    {
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-3003", [NewActiveItem()]);

        var act = () => order.CancelItem(Guid.NewGuid(), reason: "wrong table");

        act.Should().Throw<ArgumentException>().WithParameterName("orderItemId");
    }

    private static OrderItem NewActiveItem()
        => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "İskender", 1, 120m, 10m,
            status: OrderItemState.Active, kitchenState: KitchenState.NotSent);
}

/// <summary>V1-KIT-005: OrderItem/Order's side of the kitchen state mirror.</summary>
public class OrderKitchenStateSyncTests
{
    [Fact]
    public void AdvanceKitchenStateMovesAnActiveItemWithoutTouchingStatus()
    {
        var item = NewActiveItem(KitchenState.NotSent);

        var advanced = item.AdvanceKitchenState(KitchenState.Sent);

        advanced.KitchenState.Should().Be(KitchenState.Sent);
        advanced.Status.Should().Be(OrderItemState.Active);
    }

    [Theory]
    [InlineData(OrderItemState.Draft)]
    [InlineData(OrderItemState.Cancelled)]
    [InlineData(OrderItemState.Complimentary)]
    public void AdvanceKitchenStateRejectsANonActiveItem(OrderItemState status)
    {
        var item = new OrderItem(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Kunefe", 1, 60m, 10m,
            status: status, kitchenState: KitchenState.NotSent);

        var act = () => item.AdvanceKitchenState(KitchenState.Preparing);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OrderAdvanceItemKitchenStateUpdatesTheMatchingItem()
    {
        var itemId = Guid.NewGuid();
        var item = NewActiveItem(KitchenState.Sent, itemId);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-4001", [item]);

        var updated = order.AdvanceItemKitchenState(itemId, KitchenState.Preparing);

        updated.Items.Single().KitchenState.Should().Be(KitchenState.Preparing);
        updated.Items.Single().Status.Should().Be(OrderItemState.Active);
    }

    [Fact]
    public void RedeliveryOfTheSameStateIsANoOpAndReturnsTheSameInstance()
    {
        var itemId = Guid.NewGuid();
        var item = NewActiveItem(KitchenState.Preparing, itemId);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-4002", [item]);

        var result = order.AdvanceItemKitchenState(itemId, KitchenState.Preparing);

        result.Should().BeSameAs(order, "an at-least-once redelivery of an already-applied state must not rewrite the row");
    }

    [Fact]
    public void AVoidedItemNoLongerReceivesKitchenStateUpdates()
    {
        var itemId = Guid.NewGuid();
        var item = new OrderItem(
            itemId, Guid.NewGuid(), Guid.NewGuid(), "Mercimek Corbasi", 1, 30m, 10m,
            status: OrderItemState.Cancelled, kitchenState: KitchenState.Cancelled);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-4003", [item]);

        var result = order.AdvanceItemKitchenState(itemId, KitchenState.Ready);

        result.Should().BeSameAs(order, "a stale kitchen event for an item voided in the meantime must not resurrect it");
    }

    [Fact]
    public void UnknownItemIdThrows()
    {
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-4004", [NewActiveItem(KitchenState.NotSent)]);

        var act = () => order.AdvanceItemKitchenState(Guid.NewGuid(), KitchenState.Preparing);

        act.Should().Throw<ArgumentException>().WithParameterName("orderItemId");
    }

    private static OrderItem NewActiveItem(KitchenState kitchenState, Guid? id = null)
        => new(
            id ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Adana Kebap", 1, 150m, 10m,
            status: OrderItemState.Active, kitchenState: kitchenState);
}
