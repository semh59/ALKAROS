using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.PortionReservations.CancellationEffects;
using ALKAROS.Inventory.PortionReservations.Lifecycle;
using ALKAROS.Inventory.ReservationBalanceProjection;
using ALKAROS.Inventory.WasteRecording;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace ALKAROS.Inventory.CrossChannelReservation.Tests;

/// <summary>
/// V1-RMD-310: a compensation is one transaction. Rolled back with its caller it leaves nothing behind; while it
/// is open a competing sale waits and then finds the portion gone; a repeat completes what an interrupted earlier
/// run left undone; and without a caller transaction the waste still leaves on-hand before the hold is lowered,
/// so available stock never rises on the way.
/// </summary>
public sealed class AtomicCompensationTests : IClassFixture<CrossChannelReservationTestDatabase>
{
    private readonly CrossChannelReservationTestDatabase _db;
    private readonly PostgresCrossChannelPortionArbiter _arbiter;

    public AtomicCompensationTests(CrossChannelReservationTestDatabase db)
    {
        _db = db;
        _arbiter = db.CreateArbiter();
    }

    private static CrossChannelReservationRequest Request(Guid productId) =>
        new(ReservationChannel.Online, $"ys-{Guid.NewGuid():N}", Guid.NewGuid(), Guid.NewGuid(),
            [new CrossChannelReservationLine(Guid.NewGuid(), productId, 1m)]);

    private Task<CrossChannelReservationResult> ReserveCommittedAsync(CrossChannelReservationRequest request) =>
        _db.InTransactionAsync((connection, transaction) => _arbiter.ReserveAsync(request, connection, transaction));

    private async Task<string> ReservationStatusAsync(Guid orderId)
    {
        await using var cmd = _db.DataSource.CreateCommand("SELECT status FROM inventory.portion_reservations WHERE order_id = $1;");
        cmd.Parameters.AddWithValue(orderId);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<long> WasteRecordsForAsync(Guid orderId)
    {
        await using var cmd = _db.DataSource.CreateCommand(
            """
            SELECT count(*) FROM inventory.waste_records w
            JOIN inventory.portion_reservations r ON r.id = w.source_reference_id
            WHERE r.order_id = $1;
            """);
        cmd.Parameters.AddWithValue(orderId);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task SetReservationStatusAsync(Guid orderId, string status)
    {
        await using var cmd = _db.DataSource.CreateCommand(
            "UPDATE inventory.portion_reservations SET status = $2, version = version + 1, transitioned_at = now() WHERE order_id = $1;");
        cmd.Parameters.AddWithValue(orderId);
        cmd.Parameters.AddWithValue(status);
        await cmd.ExecuteNonQueryAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ACompensationRolledBackWithItsCallerLeavesTheHoldExactlyAsItWas(bool kitchenStarted)
    {
        var stock = await _db.SeedStockAsync(onHand: 1m);
        var request = Request(stock.ProductId);
        await ReserveCommittedAsync(request);
        if (kitchenStarted)
            await _db.MarkKitchenStartedAsync(request.Lines[0].OrderItemId);

        var decisions = await _db.InTransactionAsync(
            (connection, transaction) => _arbiter.CompensateAsync(request.OrderId, request.ActorId, "İptal", connection, transaction),
            rollback: true);

        decisions.Should().ContainSingle().Which.Action.Should().Be(kitchenStarted ? CancellationAction.Waste : CancellationAction.Release);
        (await _db.BalanceAsync(stock)).Should().Be((1m, 1m, 0m));
        (await ReservationStatusAsync(request.OrderId)).Should().Be("Reserved");
        (await WasteRecordsForAsync(request.OrderId)).Should().Be(0);
    }

    [Fact]
    public async Task WhileAWasteIsBeingRecordedACompetingSaleWaitsAndThenFindsThePortionGone()
    {
        var stock = await _db.SeedStockAsync(onHand: 1m);
        var request = Request(stock.ProductId);
        await ReserveCommittedAsync(request);
        await _db.MarkKitchenStartedAsync(request.Lines[0].OrderItemId);
        var guard = new PostgresReservationAwareConsumptionGuard();

        await using var connection = await _db.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await _arbiter.CompensateAsync(request.OrderId, request.ActorId, "İptal", connection, transaction);

        var sale = _db.InTransactionAsync(async (saleConnection, saleTransaction) =>
        {
            await _db.Balances.AcquireOnHandLockAsync(stock.StockItemId, stock.LocationId, saleConnection, saleTransaction);
            return await guard.ConvertOwnHoldsAndCheckAvailableAsync(
                Guid.NewGuid(), stock.StockItemId, stock.LocationId, 1m, Guid.NewGuid(), saleConnection, saleTransaction);
        });
        var finishedEarly = await Task.WhenAny(sale, Task.Delay(500)) == sale;
        await transaction.CommitAsync();

        finishedEarly.Should().BeFalse("the compensation holds the stock row until it commits");
        (await sale).Should().BeFalse();
        (await _db.BalanceAsync(stock)).Should().Be((0m, 0m, 0m));
        (await WasteRecordsForAsync(request.OrderId)).Should().Be(1);
    }

    [Fact]
    public async Task AReleaseInterruptedBeforeItsProjectionIsCompletedByTheRepeatExactlyOnce()
    {
        var stock = await _db.SeedStockAsync(onHand: 1m);
        var request = Request(stock.ProductId);
        await ReserveCommittedAsync(request);
        await SetReservationStatusAsync(request.OrderId, "Released");

        var repeat = await _arbiter.CompensateAsync(request.OrderId, request.ActorId, "İptal");
        await _arbiter.CompensateAsync(request.OrderId, request.ActorId, "İptal");

        repeat.Should().ContainSingle().Which.IsIdempotentReplay.Should().BeTrue();
        (await _db.BalanceAsync(stock)).Should().Be((1m, 0m, 1m));
    }

    [Fact]
    public async Task AWasteInterruptedBeforeItsMovementIsCompletedByTheRepeatExactlyOnce()
    {
        var stock = await _db.SeedStockAsync(onHand: 2m);
        var request = Request(stock.ProductId);
        await ReserveCommittedAsync(request);
        await SetReservationStatusAsync(request.OrderId, "Waste");

        await _arbiter.CompensateAsync(request.OrderId, request.ActorId, "İptal");
        await _arbiter.CompensateAsync(request.OrderId, request.ActorId, "İptal");

        (await WasteRecordsForAsync(request.OrderId)).Should().Be(1);
        (await _db.BalanceAsync(stock)).Should().Be((1m, 0m, 1m));
    }

    [Fact]
    public async Task AWasteAnEarlierRunRecordedUnderAnotherKeyIsNeverRecordedAgain()
    {
        var stock = await _db.SeedStockAsync(onHand: 3m);
        var request = Request(stock.ProductId);
        await ReserveCommittedAsync(request);
        var reservationId = await ReservationIdAsync(request.OrderId);
        await SetReservationStatusAsync(request.OrderId, "Waste");
        await _db.CreateWasteService().RecordWasteAsync(new RecordWasteRequest(
            stock.StockItemId, stock.LocationId, WasteSources.PortionReservation, 1m, "portion", "Önceki çalışma",
            request.ActorId, SourceReferenceId: reservationId, IdempotencyKey: "an-earlier-run"));

        await _arbiter.CompensateAsync(request.OrderId, request.ActorId, "İptal");

        (await WasteRecordsForAsync(request.OrderId)).Should().Be(1);
        (await _db.BalanceAsync(stock)).Should().Be((2m, 0m, 2m));
    }

    [Fact]
    public async Task WithoutACallerTransactionTheWasteLeavesOnHandBeforeTheHoldIsLowered()
    {
        var stock = await _db.SeedStockAsync(onHand: 1m);
        var request = Request(stock.ProductId);
        await ReserveCommittedAsync(request);
        await _db.MarkKitchenStartedAsync(request.Lines[0].OrderItemId);
        var reservationId = await ReservationIdAsync(request.OrderId);

        var observing = new ObservingProjector(_db.CreateProjector(), _db.Balances, stock);
        var service = _db.CreateCancellationService(observing);

        var decision = await service.ProcessCancellationAsync(
            new ProcessCancellationCommand(reservationId, request.Lines[0].OrderItemId, request.ActorId, "İptal"));

        decision.Action.Should().Be(CancellationAction.Waste);
        observing.OnHandWhenTheHoldWasLowered.Should().Be(0m, "the waste movement is written first");
        (await _db.BalanceAsync(stock)).Should().Be((0m, 0m, 0m));
    }

    [Fact]
    public async Task ACompensationLocksStockRowsInTheSameOrderAsAReservationWhateverTheHoldOrder()
    {
        var first = await _db.SeedStockAsync(onHand: 100m);
        var second = await _db.SeedStockAsync(onHand: 100m);
        var (low, high) = first.StockItemId.CompareTo(second.StockItemId) < 0 ? (first, second) : (second, first);

        // An order whose holds come back high-row first, so a compensation that simply followed the holds would
        // lock the high row before the low one.
        CrossChannelReservationRequest? request = null;
        for (var attempt = 0; attempt < 40 && request is null; attempt++)
        {
            var candidate = new CrossChannelReservationRequest(
                ReservationChannel.Online, $"ys-{Guid.NewGuid():N}", Guid.NewGuid(), Guid.NewGuid(),
                [new CrossChannelReservationLine(Guid.NewGuid(), low.ProductId, 1m),
                 new CrossChannelReservationLine(Guid.NewGuid(), high.ProductId, 1m)]);
            await ReserveCommittedAsync(candidate);
            await using var cmd = _db.DataSource.CreateCommand(
                "SELECT stock_item_id FROM inventory.portion_reservations WHERE order_id = $1 ORDER BY id LIMIT 1;");
            cmd.Parameters.AddWithValue(candidate.OrderId);
            if ((Guid)(await cmd.ExecuteScalarAsync())! == high.StockItemId)
                request = candidate;
        }
        request.Should().NotBeNull();
        foreach (var line in request!.Lines)
            await _db.MarkKitchenStartedAsync(line.OrderItemId);

        await using var blocker = await _db.DataSource.OpenConnectionAsync();
        await using var blockerTransaction = await blocker.BeginTransactionAsync();
        await _db.Balances.AcquireOnHandLockAsync(low.StockItemId, low.LocationId, blocker, blockerTransaction);

        var compensation = _db.InTransactionAsync((connection, transaction) =>
            _arbiter.CompensateAsync(request.OrderId, request.ActorId, "İptal", connection, transaction));
        await Task.Delay(500);

        bool highRowFree;
        await using (var probe = await _db.DataSource.OpenConnectionAsync())
        await using (var probeTransaction = await probe.BeginTransactionAsync())
        {
            await using var tryLock = new NpgsqlCommand("SELECT pg_try_advisory_xact_lock(hashtext($1)::bigint);", probe, probeTransaction);
            tryLock.Parameters.AddWithValue($"{high.StockItemId:N}:{high.LocationId:N}");
            highRowFree = (bool)(await tryLock.ExecuteScalarAsync())!;
            await probeTransaction.RollbackAsync();
        }

        await blockerTransaction.RollbackAsync();
        var decisions = await compensation;

        highRowFree.Should().BeTrue("the compensation waits on the low row before it touches the high one");
        decisions.Should().HaveCount(2).And.OnlyContain(d => d.Action == CancellationAction.Waste);
    }

    private async Task<Guid> ReservationIdAsync(Guid orderId)
    {
        await using var cmd = _db.DataSource.CreateCommand("SELECT id FROM inventory.portion_reservations WHERE order_id = $1;");
        cmd.Parameters.AddWithValue(orderId);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>Records on-hand at the moment the reserved quantity is lowered.</summary>
    private sealed class ObservingProjector(
        IReservationBalanceProjector inner, PostgresStockBalanceRepository balances, StockFixture stock) : IReservationBalanceProjector
    {
        public decimal? OnHandWhenTheHoldWasLowered { get; private set; }

        public async Task<ApplyReservationResult> ApplyTerminalInTransactionAsync(
            PortionReservation reservation, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct = default)
        {
            OnHandWhenTheHoldWasLowered = (await balances.GetByItemAndLocationAsync(stock.StockItemId, stock.LocationId, ct))!.OnHandQuantity;
            return await inner.ApplyTerminalInTransactionAsync(reservation, connection, transaction, ct);
        }

        public Task<ApplyReservationResult> ApplyReservationCreatedAsync(PortionReservation reservation, CancellationToken ct = default) =>
            inner.ApplyReservationCreatedAsync(reservation, ct);

        public Task<ApplyReservationResult> ApplyReservationTransitionAsync(
            PortionReservation reservation, PortionReservationStatus previousStatus, CancellationToken ct = default) =>
            inner.ApplyReservationTransitionAsync(reservation, previousStatus, ct);

        public Task<ReservationBalanceRebuildReport> RebuildReservationBalancesAsync(CancellationToken ct = default) =>
            inner.RebuildReservationBalancesAsync(ct);

        public Task<ReservationBalanceDriftReport> DetectDriftAsync(CancellationToken ct = default) => inner.DetectDriftAsync(ct);
    }
}
