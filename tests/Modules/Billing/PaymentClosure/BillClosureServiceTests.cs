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
    public async Task TheBillIsReadRebuiltAndSavedUnderItsSettlementLockAndTheLockIsReleased()
    {
        var bill = MakeBill();
        var repository = new FakeBills(bill);
        var projector = new FakeProjector(bill, satisfied: true) { LockProbe = () => repository.LockHeld };
        var service = new BillClosureService(repository, projector);

        var result = await service.TryCloseAsync(bill.Id);

        result.Outcome.Should().Be(BillClosureOutcome.Closed);
        repository.LocksTaken.Should().Be(1);
        repository.LockHeldAtAccess.Should().NotBeEmpty().And.OnlyContain(held => held);
        projector.LockHeldAtRebuild.Should().Equal(true);
        repository.LockHeld.Should().BeFalse();
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
        public Func<bool>? LockProbe { get; init; }

        public List<bool> LockHeldAtRebuild { get; } = [];

        public Task<BillPaymentClosureProjection> RebuildAsync(Guid billId, CancellationToken cancellationToken = default)
        {
            LockHeldAtRebuild.Add(LockProbe?.Invoke() ?? false);
            return Task.FromResult(new BillPaymentClosureProjection(
                bill.Id, "TRY", bill.PayableAmount, bill.PayableAmount, bill.PayableAmount, 0m, satisfied, blockers));
        }
    }

    private sealed class FakeBills(Bill bill, int failedSaves = 0) : IBillRepository
    {
        public int SaveAttempts { get; private set; }

        public Bill? Saved { get; private set; }

        public bool LockHeld { get; private set; }

        public int LocksTaken { get; private set; }

        public List<bool> LockHeldAtAccess { get; } = [];

        public Task<IAsyncDisposable> AcquireSettlementLockAsync(Guid billId, CancellationToken cancellationToken = default)
        {
            LocksTaken++;
            LockHeld = true;
            return Task.FromResult<IAsyncDisposable>(new Release(this));
        }

        private sealed class Release(FakeBills owner) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                owner.LockHeld = false;
                return ValueTask.CompletedTask;
            }
        }

        public Task AddAsync(Bill added, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Bill?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            LockHeldAtAccess.Add(LockHeld);
            return Task.FromResult<Bill?>(bill);
        }

        public Task<long> SaveAsync(Bill updated, long expectedRowVersion, CancellationToken cancellationToken = default)
        {
            SaveAttempts++;
            LockHeldAtAccess.Add(LockHeld);
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

        public Task<int> CancelOpenBillsOfOrderAsync(Guid orderId, string reason, Guid cancelledBy, DateTimeOffset timestamp, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> CancelBillAsync(Guid billId, string reason, Guid cancelledBy, DateTimeOffset timestamp, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
