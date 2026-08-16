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

    [Fact]
    public void PreparedActiveItemCannotBeVoided()
    {
        var item = NewItem(OrderItemState.Active, KitchenState.Preparing);

        var act = () => item.Cancel();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Order item {item.Id} cannot be voided after preparation (Preparing).");
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
    public void VoidIncompatibleItemThrows()
    {
        var itemId = Guid.NewGuid();
        var item = new OrderItem(
            itemId, Guid.NewGuid(), Guid.NewGuid(), "Hamburger", 1, 80m, 10m,
            status: OrderItemState.Active, kitchenState: KitchenState.Preparing);
        var order = new Order(Guid.NewGuid(), OrderSource.Waiter, "ORD-3002", [item], status: OrderState.Preparing);

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