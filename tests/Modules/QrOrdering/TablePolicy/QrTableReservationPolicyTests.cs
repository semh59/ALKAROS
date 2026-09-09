using ALKAROS.Tables.TableLifecycle;
using Npgsql;
using Xunit;

namespace ALKAROS.QrOrdering.TablePolicy.Tests;

/// <summary>
/// V12-QRO-002: QrTableReservationPolicy's own decision logic, isolated from
/// the full QrPendingOrderStore.SubmitAsync pipeline (which already covers
/// this against a real Postgres database in
/// ALKAROS.QrOrdering.PendingOrders.Tests) — specifically the two paths that
/// pipeline can't reach through its own seed helpers: a disabled table, and
/// a table that does not exist at all. Both use a fake ITableRepository (no
/// real Postgres needed) since the class under test never touches SQL
/// itself — it only orchestrates GetByIdForUpdateAsync/UpdateStatusAsync.
/// </summary>
public sealed class QrTableReservationPolicyTests
{
    [Fact]
    public async Task AnAvailableTableIsReservedAtomically()
    {
        var table = new Table(Guid.NewGuid(), "1", state: TableState.Available);
        var repository = new FakeTableRepository(table);
        var policy = new ALKAROS.QrOrdering.TablePolicy.QrTableReservationPolicy(repository);

        await policy.ReserveForSubmissionAsync(table.Id, FakeConnection, FakeTransaction, CancellationToken.None);

        Assert.Equal(TableState.Reserved, repository.LastUpdatedTarget);
    }

    [Theory]
    [InlineData(TableState.Occupied)]
    [InlineData(TableState.Reserved)]
    [InlineData(TableState.Cleaning)]
    [InlineData(TableState.OutOfService)]
    public async Task ANonAvailableTableIsRefusedAndNeverUpdated(TableState currentState)
    {
        var table = new Table(Guid.NewGuid(), "2", state: currentState);
        var repository = new FakeTableRepository(table);
        var policy = new ALKAROS.QrOrdering.TablePolicy.QrTableReservationPolicy(repository);

        var exception = await Assert.ThrowsAsync<QrTableNotAvailableException>(
            () => policy.ReserveForSubmissionAsync(table.Id, FakeConnection, FakeTransaction, CancellationToken.None));

        Assert.Equal(currentState, exception.CurrentState);
        Assert.False(repository.WasUpdateCalled);
    }

    [Fact]
    public async Task ADisabledTableIsTreatedAsNotFound()
    {
        var table = new Table(Guid.NewGuid(), "3", active: false, state: TableState.Available);
        var repository = new FakeTableRepository(table);
        var policy = new ALKAROS.QrOrdering.TablePolicy.QrTableReservationPolicy(repository);

        await Assert.ThrowsAsync<QrTableNotFoundException>(
            () => policy.ReserveForSubmissionAsync(table.Id, FakeConnection, FakeTransaction, CancellationToken.None));
        Assert.False(repository.WasUpdateCalled);
    }

    [Fact]
    public async Task AMissingTableIsNotFound()
    {
        var repository = new FakeTableRepository(table: null);
        var policy = new ALKAROS.QrOrdering.TablePolicy.QrTableReservationPolicy(repository);

        await Assert.ThrowsAsync<QrTableNotFoundException>(
            () => policy.ReserveForSubmissionAsync(Guid.NewGuid(), FakeConnection, FakeTransaction, CancellationToken.None));
    }

    // A real connection/transaction is never actually used by the fake
    // repository below — Npgsql requires non-null references for the
    // parameter types, so these are opened but never queried against.
    private static NpgsqlConnection FakeConnection { get; } = new();
    private static NpgsqlTransaction FakeTransaction => null!;

    private sealed class FakeTableRepository : ITableRepository
    {
        private readonly Table? _table;

        public FakeTableRepository(Table? table) => _table = table;

        public bool WasUpdateCalled { get; private set; }
        public TableState? LastUpdatedTarget { get; private set; }

        public Task<Table?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_table);

        public Task<IReadOnlyList<Table>> GetByZoneAsync(Guid zoneId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Table>> GetUnzonedAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAsync(Table table, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<long> UpdateStatusAsync(
            Guid id, TableState target, long expectedRowVersion, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Table?> GetByIdForUpdateAsync(
            Guid id, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default) =>
            Task.FromResult(_table is not null && _table.Id == id ? _table : null);

        public Task<long> UpdateStatusAsync(
            Guid id,
            TableState target,
            long expectedRowVersion,
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            WasUpdateCalled = true;
            LastUpdatedTarget = target;
            return Task.FromResult(expectedRowVersion + 1);
        }

        public Task LinkCurrentOrderAsync(
            Guid tableId, Guid orderId, NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
