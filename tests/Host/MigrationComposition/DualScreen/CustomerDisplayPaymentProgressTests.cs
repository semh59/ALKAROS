using ALKAROS.Billing.PaymentClosure;
using ALKAROS.Host.DualScreen;
using Xunit;

namespace ALKAROS.Host.Tests.DualScreen;

public sealed class CustomerDisplayPaymentProgressTests
{
    private static CustomerDisplaySnapshotDto Snapshot(string state = "Active") => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 4, state, false, "POS-4", [],
        236.36m, 0m, 23.64m, 260m, "TRY", DateTimeOffset.UtcNow, "Siparişiniz işleniyor.");

    private static BillPaymentClosureProjection Projection(decimal payable, decimal allocated) => new(
        Guid.NewGuid(), "TRY", payable, allocated, allocated, 0m, allocated >= payable, []);

    [Fact]
    public void ABillWithoutPaymentsShowsItsPayableAmountAsRemaining()
    {
        var result = CustomerDisplayPaymentProgress.Apply(Snapshot(), [Projection(250m, 0m)]);

        Assert.Equal(0m, result.PaidTotal);
        Assert.Equal(250m, result.RemainingTotal);
        Assert.Equal("Siparişiniz işleniyor.", result.Message);
    }

    [Fact]
    public void APartialPaymentShowsPaidAndRemainingAndSaysSo()
    {
        var result = CustomerDisplayPaymentProgress.Apply(Snapshot(), [Projection(260m, 100m)]);

        Assert.Equal(100m, result.PaidTotal);
        Assert.Equal(160m, result.RemainingTotal);
        Assert.Equal(CustomerDisplayPaymentProgress.PartiallyPaidMessage, result.Message);
        Assert.Equal(260m, result.Total);
    }

    [Fact]
    public void ABillCoveredInFullLeavesNothingRemainingAndKeepsTheOrderMessage()
    {
        var result = CustomerDisplayPaymentProgress.Apply(Snapshot(), [Projection(260m, 260m)]);

        Assert.Equal(260m, result.PaidTotal);
        Assert.Equal(0m, result.RemainingTotal);
        Assert.Equal("Siparişiniz işleniyor.", result.Message);
    }

    [Fact]
    public void AllocationAboveThePayableAmountNeverShowsANegativeRemainder()
    {
        var result = CustomerDisplayPaymentProgress.Apply(Snapshot(), [Projection(100m, 130m)]);

        Assert.Equal(0m, result.RemainingTotal);
    }

    [Fact]
    public void SeveralBillsOfOneOrderAreSummed()
    {
        var result = CustomerDisplayPaymentProgress.Apply(Snapshot(), [Projection(100m, 40m), Projection(160m, 0m)]);

        Assert.Equal(40m, result.PaidTotal);
        Assert.Equal(220m, result.RemainingTotal);
    }

    [Fact]
    public void WithoutABillTheSnapshotIsUnchanged()
    {
        var snapshot = Snapshot();

        Assert.Equal(snapshot, CustomerDisplayPaymentProgress.Apply(snapshot, []));
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Idle")]
    public async Task ACompletedOrIdleScreenNeverReadsTheBills(string state)
    {
        var snapshot = Snapshot(state);

        // Null collaborators: a read of bills or payments would throw.
        Assert.Equal(snapshot, await CustomerDisplayPaymentProgress.AttachAsync(snapshot, null!, null!, CancellationToken.None));
    }

    [Fact]
    public async Task ASnapshotWithoutAnOrderNeverReadsTheBills()
    {
        var snapshot = Snapshot() with { OrderId = null };

        Assert.Equal(snapshot, await CustomerDisplayPaymentProgress.AttachAsync(snapshot, null!, null!, CancellationToken.None));
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Idle")]
    public void ACompletedOrIdleScreenIsNotTouched(string state)
    {
        var snapshot = Snapshot(state);

        Assert.Equal(snapshot, CustomerDisplayPaymentProgress.Apply(snapshot, [Projection(260m, 100m)]));
    }
}
