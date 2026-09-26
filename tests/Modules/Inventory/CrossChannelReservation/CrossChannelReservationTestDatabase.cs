using ALKAROS.Inventory.BalanceProjection;
using ALKAROS.Inventory.MovementLedger;
using ALKAROS.Inventory.PortionReservations.CancellationEffects;
using ALKAROS.Inventory.PortionReservations.Lifecycle;
using ALKAROS.Inventory.ReservationBalanceProjection;
using ALKAROS.Inventory.StockMaster;
using ALKAROS.Inventory.Transactions;
using ALKAROS.Inventory.WasteRecording;
using ALKAROS.Measurements;
using ALKAROS.TestHelpers;
using Npgsql;

namespace ALKAROS.Inventory.CrossChannelReservation.Tests;

public sealed class CrossChannelReservationTestDatabase : PgTestDatabase
{
    public CrossChannelReservationTestDatabase() : base("alkaros_ccr_test_") { }

    protected override async Task ApplySqlAsync()
    {
        foreach (var file in new[]
                 {
                     "059-stock-master.up.sql",
                     "118-stock-items-reorder-point.up.sql",
                     "060-stock-movements.up.sql",
                     "061-stock-balances.up.sql",
                     "063-waste-records.up.sql",
                     "064-portion-reservations.up.sql",
                     "065-reservation-balance-projection.up.sql",
                     "087-inventory-stock-balances-non-negative.up.sql"
                 })
        {
            await RunAsync(DataSource, await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sql", file)));
        }

        // Only the columns PostgresKitchenItemStateProvider reads (same minimal shape as
        // V11-RSV-003's own tests): the Kitchen module's real table references orders.orders.
        await RunAsync(DataSource, """
            CREATE SCHEMA IF NOT EXISTS kitchen;
            CREATE TABLE IF NOT EXISTS kitchen.kitchen_ticket_items (
                id UUID PRIMARY KEY,
                order_item_id UUID NOT NULL,
                status VARCHAR(32) NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );
            """);
    }

    public PostgresStockBalanceRepository Balances => new(DataSource);

    public PostgresCrossChannelPortionArbiter CreateArbiter() =>
        new(DataSource, Balances, CreateCancellationService(CreateProjector()));

    public WasteRecordingService CreateWasteService() => new(
        new PostgresInventoryTransactionRunner(DataSource),
        new PostgresWasteRecordRepository(DataSource),
        new PostgresStockMovementRepository(DataSource),
        new PostgresStockItemRepository(DataSource),
        new PostgresStockLocationRepository(DataSource),
        Balances,
        new UnitConverter());

    /// <summary>The real cancellation decision service over this database, with the given projector (V1-RMD-310).</summary>
    public PortionCancellationDecisionService CreateCancellationService(IReservationBalanceProjector projector)
    {
        var items = new PostgresStockItemRepository(DataSource);
        var locations = new PostgresStockLocationRepository(DataSource);
        var reservations = new PostgresPortionReservationRepository(DataSource);
        return new PortionCancellationDecisionService(
            reservations,
            new PortionReservationLifecycleService(reservations, items, locations),
            projector,
            CreateWasteService(),
            new PostgresKitchenItemStateProvider(DataSource));
    }

    public ReservationBalanceProjector CreateProjector() => new(new PostgresReservationBalanceRepository(DataSource));

    /// <summary>A location, a stock item tracked in portions at that location, a product mapped to it, and an on-hand balance.</summary>
    public async Task<StockFixture> SeedStockAsync(decimal onHand, decimal multiplier = 1m, bool withDefaultLocation = true)
    {
        var master = new StockMasterService(
            new PostgresStockLocationRepository(DataSource),
            new PostgresStockItemRepository(DataSource),
            new PostgresProductStockMappingRepository(DataSource),
            new UnitConverter());
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var location = await master.CreateLocationAsync("LOC-" + suffix, "Pass", StockLocationType.Kitchen);
        var item = await master.CreateStockItemAsync(
            "SKU-" + suffix, "Son Porsiyon Levrek", StockItemType.RawMaterial, "portion",
            defaultLocationId: withDefaultLocation ? location.Id : null);
        var productId = Guid.NewGuid();
        await master.AssignProductToStockItemAsync(productId, item.Id, multiplier);
        if (onHand > 0m)
            await Balances.ApplyOnHandDeltaAsync(item.Id, location.Id, onHand);
        return new StockFixture(productId, item.Id, location.Id);
    }

    public async Task MarkKitchenStartedAsync(Guid orderItemId)
    {
        await using var cmd = DataSource.CreateCommand(
            "INSERT INTO kitchen.kitchen_ticket_items (id, order_item_id, status) VALUES ($1, $2, 'Preparing');");
        cmd.Parameters.AddWithValue(Guid.NewGuid());
        cmd.Parameters.AddWithValue(orderItemId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<(decimal OnHand, decimal Reserved, decimal Available)> BalanceAsync(StockFixture stock)
    {
        var balance = await Balances.GetByItemAndLocationAsync(stock.StockItemId, stock.LocationId)
            ?? throw new InvalidOperationException("No balance row.");
        return (balance.OnHandQuantity, balance.ReservedQuantity, balance.AvailableQuantity);
    }

    public async Task<IReadOnlyList<(Guid OrderId, string Status, string? Metadata)>> ReservationsForAsync(Guid stockItemId)
    {
        await using var cmd = DataSource.CreateCommand(
            "SELECT order_id, status, metadata::text FROM inventory.portion_reservations WHERE stock_item_id = $1 ORDER BY reserved_at;");
        cmd.Parameters.AddWithValue(stockItemId);
        var rows = new List<(Guid, string, string?)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add((reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
        return rows;
    }

    /// <summary>Runs <paramref name="work"/> in its own connection and transaction, committing unless told to roll back.</summary>
    public async Task<T> InTransactionAsync<T>(
        Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> work,
        bool rollback = false)
    {
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var result = await work(connection, transaction);
        if (rollback)
            await transaction.RollbackAsync();
        else
            await transaction.CommitAsync();
        return result;
    }
}

public sealed record StockFixture(Guid ProductId, Guid StockItemId, Guid LocationId);
