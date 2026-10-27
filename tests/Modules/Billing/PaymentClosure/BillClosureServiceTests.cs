using ALKAROS.Billing.BillFoundation;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Billing.PaymentClosure.Tests;

public sealed class BillClosureServiceTests
{
    [Fact]
    public async Task AnUnresolvedPaymentKeepsTheBillOpenEvenWhenTheAmountIsCovered()
    {
        var bill = MakeBill();
        var repository = new FakeBills(bill);
        var service = new BillClosureService(repository, new FakeProjector(bill, satisfied: true, BillClosureBlocker.HasUnknownPayment));

        var result = await service.TryCloseAsync(bill.Id);

        result.Outcome.Should().Be(BillClosureOutcome.NotSatisfied);
        result.Blockers.Should().Equal(BillClosureBlocker.HasUnknownPayment);
        repository.SaveAttempts.Should().Be(0);
    }

    [Fact]
    public async Task ABillThatChangedUnderTheCloserIsRetriedAndThenClosed()
    {
        var bill = MakeBill();
        var repository = new FakeBills(bill, failedSaves: 2);
        var service = new BillClosureService(repository, new FakeProjector(bill, satisfied: true));

        var result = await service.TryCloseAsync(bill.Id);

        result.Outcome.Should().Be(BillClosureOutcome.Closed);
        repository.SaveAttempts.Should().Be(3);
        repository.Saved!.Status.Should().Be(BillState.Paid);
    }

    [Fact]
    public async Task ABillThatKeepsChangingIsGivenUpAfterThreeAttempts()
    {
        var bill = MakeBill();
        var repository = new FakeBills(bill, failedSaves: int.MaxValue);
        var service = new BillClosureService(repository, new FakeProjector(bill, satisfied: true));

        var close = () => service.TryCloseAsync(bill.Id);

        await close.Should().ThrowAsync<InvalidOperationException>();
        repository.SaveAttempts.Should().Be(3);
    }

    [Theory]
    [InlineData(BillState.Paid, BillClosureOutcome.AlreadyClosed)]
    [InlineData(BillState.Cancelled, BillClosureOutcome.NotClosable)]
    public async Task APaidOrCancelledBillIsNotTouched(BillState state, BillClosureOutcome expected)
    {
        var bill = new Bill(Guid.NewGuid(), "BILL-FINAL", status: state);
        var repository = new FakeBills(bill);
        var service = new BillClosureService(repository, new FakeProjector(bill, satisfied: true));

        var result = await service.TryCloseAsync(bill.Id);

        result.Outcome.Should().Be(expected);
        repository.SaveAttempts.Should().Be(0);
    }

    private static Bill MakeBill()
    {
        var billId = Guid.NewGuid();
        var item = new BillItem(Guid.NewGuid(), billId, Guid.NewGuid(), Guid.NewGuid(), "Item", 1, 100m, 0m);
        return new Bill(billId, "BILL-CLOSE", items: [item], currencyCode: "TRY");
    }

    private sealed class FakeProjector(Bill bill, bool satisfied, params BillClosureBlocker[] blockers) : IBillPaymentClosureProjector
    {
        public Task<BillPaymentClosureProjection> RebuildAsync(Guid billId, CancellationToken cancellationToken = default)
            => Task.FromResult(new BillPaymentClosureProjection(
                bill.Id, "TRY", bill.PayableAmount, bill.PayableAmount, bill.PayableAmount, 0m, satisfied, blockers));
    }

    private sealed class FakeBills(Bill bill, int failedSaves = 0) : IBillRepository
    {
        public int SaveAttempts { get; private set; }

        public Bill? Saved { get; private set; }

        public Task<Bill?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Bill?>(bill);

        public Task<long> SaveAsync(Bill updated, long expectedRowVersion, CancellationToken cancellationToken = default)
        {
            SaveAttempts++;
            if (SaveAttempts <= failedSaves)
                throw new InvalidOperationException("row version changed");
            Saved = updated;
            return Task.FromResult(expectedRowVersion + 1);
        }

        public Task<Bill?> GetByBillNumberAsync(string billNumber, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Bill>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Bill>> GetByTableIdAsync(Guid tableId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task AddAsync(Bill added, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<long> SaveAsync(Bill updated, long expectedRowVersion, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> IsOrderItemBilledAsync(Guid orderItemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlySet<Guid>> GetBilledOrderItemIdsAsync(IEnumerable<Guid> orderItemIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> ReparentActiveBillsToTableAsync(Guid fromTableId, Guid toTableId, DateTimeOffset timestamp, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> ReparentBillToTableAsync(Guid billId, Guid expectedFromTableId, Guid toTableId, DateTimeOffset timestamp, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> CancelActiveBillsForOrderAsync(Guid orderId, DateTimeOffset timestamp, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
